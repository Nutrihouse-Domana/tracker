package br.com.nutrihouse.tracker.agent.servico

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import br.com.nutrihouse.tracker.agent.AgenteConfig
import br.com.nutrihouse.tracker.agent.BuildConfig
import br.com.nutrihouse.tracker.agent.R
import br.com.nutrihouse.tracker.agent.atualizacao.AtualizadorAgente
import br.com.nutrihouse.tracker.agent.bloqueio.Bloqueio
import br.com.nutrihouse.tracker.agent.dados.Armazenamento
import br.com.nutrihouse.tracker.agent.dados.BackendClient
import br.com.nutrihouse.tracker.agent.dados.ComandoPendente
import br.com.nutrihouse.tracker.agent.dados.Localizacao
import br.com.nutrihouse.tracker.agent.dados.TipoAlerta
import br.com.nutrihouse.tracker.agent.dispositivo.Politica
import br.com.nutrihouse.tracker.agent.enrollment.EnrollmentActivity
import br.com.nutrihouse.tracker.agent.localizacao.Geolocalizacao
import com.microsoft.signalr.HubConnection
import com.microsoft.signalr.HubConnectionBuilder
import com.microsoft.signalr.HubConnectionState
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/**
 * Servico principal do agente — equivale ao Worker do agente Windows.
 *
 * Tres laços independentes de proposito:
 * 1. **Enforcement do bloqueio** (2s): reaplica a trava sem depender de rede nem de token. E o
 *    que faz o bloqueio sobreviver a reinicio, modo aviao, token revogado e destravamento pelo
 *    usuario.
 * 2. **Heartbeat**: telemetria, comandos pendentes e o estado de bloqueio desejado pelo
 *    servidor — a rede de seguranca caso o push nao tenha chegado.
 * 3. **SignalR**: comandos em tempo real, com reconexao feita por nos.
 */
class AgenteService : Service() {

    private val escopo = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var jobHeartbeat: Job? = null
    private var jobEnforcement: Job? = null
    private var jobHub: Job? = null
    private var hub: HubConnection? = null

    private val armazenamento by lazy { Armazenamento(this) }
    private val backend by lazy { BackendClient(this, armazenamento) }
    private val bloqueio by lazy { Bloqueio(this) }
    private val geo by lazy { Geolocalizacao(this) }
    private val politica by lazy { Politica(this) }
    private val atualizador by lazy { AtualizadorAgente(this, backend) }

    /**
     * Serializa TODA mudanca do estado de bloqueio (comando e sincronizacao pelo heartbeat). Sem
     * isso, um heartbeat que ja conferiu "nada mudou" podia desfazer um Bloquear que o hub aplicou
     * no instante seguinte.
     */
    private val mutexBloqueio = Mutex()

    /**
     * Contador monotonico de comandos Bloquear/Desbloquear aplicados neste processo. O heartbeat
     * anota o valor ANTES de sair para a rede; se ele mudou ate a resposta chegar, a resposta e
     * velha em relacao ao ultimo comando e o `deveEstarBloqueado` dela e ignorado.
     *
     * Protegido por [mutexBloqueio].
     */
    private var geracaoComandos = 0L

    /**
     * Ids ja aplicados. O mesmo comando pode chegar pelo hub E pelo heartbeat (antes de a
     * confirmacao ser registrada): reaplicar um Bloquear antigo depois de um Desbloquear mais
     * novo religaria a trava. Aqui ele so e reconfirmado. Protegido por [mutexBloqueio].
     */
    private val comandosAplicados = object : LinkedHashSet<String>() {
        fun registrar(id: String) {
            add(id)
            while (size > LIMITE_COMANDOS_LEMBRADOS) remove(first())
        }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        criarCanalDeNotificacao()

        // Uma atualizacao do sistema pode limpar restricoes: reaplica a cada inicializacao.
        runCatching { politica.aplicarEndurecimentoDeviceOwner() }

        // O enforcement sobe primeiro e nao depende de nada — nem de localizacao, nem de token.
        jobEnforcement = escopo.launch { lacoDeEnforcement() }
        jobHeartbeat = escopo.launch { lacoDeHeartbeat() }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        // Cada startForegroundService() exige um startForeground(). Refazer aqui tambem
        // reavalia o tipo: se a permissao de localizacao chegou depois (tela de registro), o
        // servico passa a declara-la.
        iniciarEmPrimeiroPlano()

        // START_STICKY: se o sistema matar o servico por memoria, ele volta sozinho.
        return START_STICKY
    }

    override fun onDestroy() {
        hub?.let { runCatching { it.stop().blockingAwait() } }
        escopo.cancel()
        super.onDestroy()
    }

    // --- Laço 1: bloqueio ---

    /**
     * Reaplica o bloqueio em ciclo curto. Roda antes de qualquer comunicacao de proposito: um
     * aparelho roubado e colocado em modo aviao continua bloqueado.
     */
    private suspend fun lacoDeEnforcement() {
        while (escopo.isActive) {
            runCatching { bloqueio.garantirAtivo() }
                .onFailure { Log.w(TAG, "Falha ao reaplicar o bloqueio.", it) }

            delay(AgenteConfig.INTERVALO_ENFORCEMENT_SEGUNDOS * 1000)
        }
    }

    // --- Laço 2: heartbeat ---

    private suspend fun lacoDeHeartbeat() {
        // Sem token nao ha o que enviar. O registro depende do codigo de instalacao, que so o
        // tecnico digita na EnrollmentActivity — o servico apenas aguarda.
        while (escopo.isActive && !armazenamento.registrado) {
            delay(AgenteConfig.INTERVALO_RETENTATIVA_SEGUNDOS * 1000)
        }
        if (!escopo.isActive) return

        // Vigia dedicado: mantém o canal em tempo real de pé sem depender do ciclo do heartbeat.
        jobHub = escopo.launch { lacoDeConexaoHub() }

        while (escopo.isActive) {
            // O token pode ser revogado no painel: nesse caso o BackendClient o apaga e o
            // aparelho volta a depender do código de instalação, digitado pelo técnico. O
            // bloqueio, se houver, continua de pé pelo laço de enforcement.
            if (!armazenamento.registrado) {
                Log.w(TAG, "Sem token: aparelho precisa ser registrado de novo no app.")
                delay(AgenteConfig.INTERVALO_RETENTATIVA_SEGUNDOS * 1000)
                continue
            }

            runCatching {
                val localizacao = geo.obter()
                if (localizacao == null && !geo.temPermissao) {
                    reportarLocalizacaoNegada()
                }

                heartbeatESincronizar(localizacao)
            }.onFailure { Log.w(TAG, "Heartbeat falhou; nova tentativa no proximo ciclo.", it) }

            delay(AgenteConfig.INTERVALO_HEARTBEAT_SEGUNDOS * 1000)
        }
    }

    /**
     * Um ciclo completo: envia o heartbeat, executa os comandos pendentes e alinha o estado de
     * bloqueio ao do servidor — nessa ordem, e com a protecao contra resposta velha.
     */
    private suspend fun heartbeatESincronizar(localizacao: Localizacao?) {
        // Carimbo tirado ANTES da ida a rede: e o que permite descobrir, na volta, se algum
        // comando (deste ciclo ou do hub, em paralelo) foi aplicado depois de o servidor calcular
        // o deveEstarBloqueado desta resposta.
        val geracaoNoInicio = mutexBloqueio.withLock { geracaoComandos }

        val resposta = backend.enviarHeartbeat(localizacao)

        resposta.comandosPendentes.forEach { processarComando(it) }

        // Estado desejado do servidor: recupera o bloqueio se o sinal local se perdeu
        // (app reinstalado, dados limpos) mesmo sem um comando novo.
        sincronizarBloqueio(resposta.deveEstarBloqueado, geracaoNoInicio)

        // Auto-atualizacao OTA: se o servidor anunciou um APK mais novo, baixa, confere
        // o hash e se instala. Roda por ultimo — bloqueio antes de tudo.
        resposta.atualizacaoAgente?.let { atualizador.processar(it) }
    }

    private var localizacaoNegadaReportada = false

    /** Reporta uma unica vez por execucao: nao faz sentido inundar o painel a cada ciclo. */
    private suspend fun reportarLocalizacaoNegada() {
        if (localizacaoNegadaReportada) return
        localizacaoNegadaReportada = true

        runCatching {
            backend.reportarAlerta(
                TipoAlerta.LocalizacaoNegada,
                "Permissão de localização ausente ou revogada no aparelho.",
            )
        }.onFailure { localizacaoNegadaReportada = false }
    }

    // --- Laço 3: comandos em tempo real ---

    /**
     * Mantém o canal em tempo real de pé. Roda em ciclo curto porque a checagem é só a leitura de
     * um estado em memória — só há custo de rede quando a conexão realmente caiu.
     *
     * É o que faz o bloqueio ser imediato: com o hub conectado, o comando chega em segundos; sem
     * ele, a entrega esperaria o próximo heartbeat.
     */
    private suspend fun lacoDeConexaoHub() {
        var esperaAposFalha = AgenteConfig.INTERVALO_VERIFICACAO_HUB_SEGUNDOS

        while (escopo.isActive) {
            if (armazenamento.registrado && hub?.connectionState != HubConnectionState.CONNECTED) {
                // Recuo progressivo até 60s: sem rede, insistir a cada 5s só gastaria bateria.
                esperaAposFalha = if (conectarHub()) {
                    AgenteConfig.INTERVALO_VERIFICACAO_HUB_SEGUNDOS
                } else {
                    (esperaAposFalha * 2).coerceAtMost(60L)
                }

                delay(esperaAposFalha * 1000)
                continue
            }

            delay(AgenteConfig.INTERVALO_VERIFICACAO_HUB_SEGUNDOS * 1000)
        }
    }

    private fun conectarHub(): Boolean {
        val token = armazenamento.token ?: return false

        return runCatching {
            hub?.let { runCatching { it.stop().blockingAwait() } }

            // PENDENTE (depende do backend): o token vai na query string porque o hub so o le
            // dali. Query string aparece em log de proxy/IIS; o ideal e enviar por header
            // (withHeader) ou accessTokenProvider assim que o backend aceitar.
            val url = BuildConfig.BACKEND_BASE_URL.trimEnd('/') +
                AgenteConfig.ROTA_HUB_COMANDOS + "?token=" + token

            hub = HubConnectionBuilder.create(url).build().apply {
                on(
                    EVENTO_EXECUTAR_COMANDO,
                    { comando: ComandoPendente ->
                        Log.i(TAG, "Comando recebido via SignalR: ${comando.tipoComando}")
                        escopo.launch { processarComando(comando) }
                    },
                    ComandoPendente::class.java,
                )

                start().blockingAwait()
            }

            Log.i(TAG, "Conectado ao hub de comandos.")

            // Um comando pode ter sido emitido enquanto estávamos fora do ar: busca as pendências
            // agora, em vez de esperar o próximo heartbeat.
            escopo.launch { buscarPendencias() }
            true
        }.onFailure {
            // Nao e fatal: o heartbeat continua entregando os comandos como rede de seguranca.
            Log.w(TAG, "Sem SignalR; operando apenas por heartbeat.", it)
        }.getOrDefault(false)
    }

    /** Consulta o backend por comandos pendentes fora do ciclo normal de telemetria. */
    private suspend fun buscarPendencias() {
        runCatching { heartbeatESincronizar(localizacao = null) }
            .onFailure { Log.w(TAG, "Falha ao buscar pendências após reconectar.", it) }
    }

    // --- Execucao de comandos ---

    private suspend fun processarComando(comando: ComandoPendente) {
        if (comando.id.isBlank()) return

        try {
            // Aceita texto ("Bloquear") e número ("0"): o heartbeat e o SignalR usam
            // serializadores diferentes no backend, e um agente que só entendesse um dos formatos
            // descartaria o comando em silêncio se as duas pontas saíssem de sincronia.
            val bloquear = when (comando.tipoComando) {
                COMANDO_BLOQUEAR, COMANDO_BLOQUEAR_NUMERICO -> true
                COMANDO_DESBLOQUEAR, COMANDO_DESBLOQUEAR_NUMERICO -> false
                else -> {
                    Log.w(TAG, "Comando desconhecido: ${comando.tipoComando}")
                    return
                }
            }

            mutexBloqueio.withLock {
                if (comando.id in comandosAplicados) {
                    // Reentrega (hub + heartbeat) de algo ja aplicado: so falta confirmar.
                    Log.i(TAG, "Comando ${comando.id} ja aplicado; apenas reconfirmando.")
                } else {
                    if (bloquear) {
                        bloqueio.aplicar()
                        politica.definirQuiosque(ativo = true)
                    } else {
                        bloqueio.remover()
                    }
                    comandosAplicados.registrar(comando.id)
                    geracaoComandos++
                }
            }

            backend.confirmarComando(comando.id)
            Log.i(TAG, "Comando ${comando.tipoComando} executado e confirmado.")
        } catch (e: Exception) {
            // Sem confirmacao o backend reentrega no proximo heartbeat — nada se perde.
            Log.w(TAG, "Falha ao executar/confirmar o comando ${comando.id}.", e)
        }
    }

    /**
     * Alinha o estado local ao que o servidor considera correto. Só age quando eles divergem:
     * reaplicar a trava a cada heartbeat geraria alertas repetidos sem motivo.
     *
     * [geracaoNoInicio] e o valor de [geracaoComandos] quando o heartbeat saiu. Se mudou, algum
     * comando foi aplicado depois de o servidor montar esta resposta (no proprio ciclo ou pelo
     * hub em paralelo) — o comando e mais novo e prevalece; a resposta e descartada.
     */
    private suspend fun sincronizarBloqueio(deveEstarBloqueado: Boolean, geracaoNoInicio: Long) {
        val reaplicado = mutexBloqueio.withLock {
            if (geracaoComandos != geracaoNoInicio) {
                Log.i(TAG, "Heartbeat anterior ao ultimo comando aplicado; estado do servidor ignorado.")
                return
            }
            if (deveEstarBloqueado == bloqueio.ativo) return

            if (deveEstarBloqueado) {
                Log.w(TAG, "Servidor indica bloqueio ativo mas o sinal local se perdeu; reaplicando.")
                bloqueio.aplicar()
                politica.definirQuiosque(ativo = true)
                true
            } else {
                // O servidor nao considera o aparelho bloqueado: a TI ja liberou.
                bloqueio.remover()
                false
            }
        }

        if (reaplicado) {
            runCatching {
                backend.reportarAlerta(
                    TipoAlerta.BloqueioReaplicado,
                    "Bloqueio reaplicado a partir do estado do servidor (sinal local ausente).",
                )
            }
        }
    }

    // --- Notificacao do servico em primeiro plano ---

    private fun criarCanalDeNotificacao() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return

        val canal = NotificationChannel(
            AgenteConfig.CANAL_NOTIFICACAO,
            getString(R.string.notificacao_canal_nome),
            // LOW: precisa ser visivel (exigencia de servico em primeiro plano) sem fazer som.
            NotificationManager.IMPORTANCE_LOW,
        ).apply {
            description = getString(R.string.notificacao_canal_descricao)
            setShowBadge(false)
        }

        (getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager)
            .createNotificationChannel(canal)
    }

    /**
     * Promove o servico a primeiro plano SEM nunca derrubar o processo.
     *
     * O tipo e escolhido em tempo de execucao. No Android 14+ declarar `location` sem permissao
     * de localizacao concedida — ou sem direito de uso "em primeiro plano", caso de quem sobe a
     * partir do BOOT_COMPLETED ou do WorkManager sem ACCESS_BACKGROUND_LOCATION — lanca
     * SecurityException. Antes, isso virava um loop de crash e o enforcement do bloqueio parava.
     *
     * Ordem de tentativa: (location se permitida) + specialUse → so specialUse → sem tipo. O
     * specialUse (Android 14+) e o tipo que descreve um agente de gestao do aparelho e nao exige
     * permissao em tempo de execucao; abaixo do 14 a checagem de permissao por tipo nao existe.
     */
    private fun iniciarEmPrimeiroPlano() {
        val notificacao = montarNotificacao()

        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) {
            runCatching { startForeground(AgenteConfig.ID_NOTIFICACAO_SERVICO, notificacao) }
                .onFailure { Log.e(TAG, "Falha ao subir o servico em primeiro plano.", it) }
            return
        }

        val tipoBase = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE
        } else {
            0
        }

        val tentativas = buildList {
            if (geo.temPermissao) add(tipoBase or ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION)
            if (tipoBase != 0) add(tipoBase)
        }

        for (tipo in tentativas) {
            try {
                startForeground(AgenteConfig.ID_NOTIFICACAO_SERVICO, notificacao, tipo)
                return
            } catch (e: Exception) {
                // SecurityException (permissao/while-in-use) ou tipo nao aceito: tenta o proximo.
                Log.w(TAG, "startForeground recusado com tipo $tipo; tentando alternativa.", e)
            }
        }

        // Ultimo recurso: usa os tipos do manifest. Abaixo do Android 14 isso nao checa permissao.
        runCatching { startForeground(AgenteConfig.ID_NOTIFICACAO_SERVICO, notificacao) }
            .onFailure {
                // Sem primeiro plano o sistema pode encerrar o servico, mas o VigilanteWorker e o
                // BootReceiver reaplicam a trava e o religam. Nunca deixamos a excecao subir.
                Log.e(TAG, "Nao foi possivel subir o servico em primeiro plano.", it)
            }
    }

    private fun montarNotificacao(): Notification {
        val abrirApp = PendingIntent.getActivity(
            this,
            0,
            Intent(this, EnrollmentActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )

        return NotificationCompat.Builder(this, AgenteConfig.CANAL_NOTIFICACAO)
            .setContentTitle(getString(R.string.notificacao_titulo))
            .setContentText(getString(R.string.notificacao_texto))
            .setSmallIcon(R.drawable.ic_tracker)
            .setOngoing(true)
            .setContentIntent(abrirApp)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build()
    }

    companion object {
        private const val TAG = "AgenteService"
        private const val EVENTO_EXECUTAR_COMANDO = "ExecutarComando"
        private const val COMANDO_BLOQUEAR = "Bloquear"
        private const val COMANDO_DESBLOQUEAR = "Desbloquear"

        // Valores do enum TipoComandoRemoto quando serializado como número.
        private const val COMANDO_BLOQUEAR_NUMERICO = "0"
        private const val COMANDO_DESBLOQUEAR_NUMERICO = "1"

        /** Quantos ids de comando lembrar para descartar reentregas. */
        private const val LIMITE_COMANDOS_LEMBRADOS = 50

        /** Sobe o servico em primeiro plano. Seguro chamar mais de uma vez. */
        fun iniciar(context: Context) {
            val intent = Intent(context, AgenteService::class.java)
            runCatching {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    context.startForegroundService(intent)
                } else {
                    context.startService(intent)
                }
            }.onFailure { Log.w(TAG, "Nao foi possivel iniciar o servico agora.", it) }
        }
    }
}
