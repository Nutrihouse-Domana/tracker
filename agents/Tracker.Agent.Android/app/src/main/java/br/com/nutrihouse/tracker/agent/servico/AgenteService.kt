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

/**
 * Servico principal do agente — equivale ao Worker do agente Windows.
 *
 * Tres laços independentes de proposito:
 * 1. **Enforcement do bloqueio** (2s): reaplica a trava sem depender de rede. E o que faz o
 *    bloqueio sobreviver a reinicio, modo aviao e destravamento pelo usuario.
 * 2. **Heartbeat** (2min): telemetria, comandos pendentes e o estado de bloqueio desejado pelo
 *    servidor — a rede de seguranca caso o push nao tenha chegado.
 * 3. **SignalR**: comandos em tempo real, com reconexao automatica.
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

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        criarCanalDeNotificacao()
        iniciarEmPrimeiroPlano()

        // Uma atualizacao do sistema pode limpar restricoes: reaplica a cada inicializacao.
        politica.aplicarEndurecimentoDeviceOwner()

        jobEnforcement = escopo.launch { lacoDeEnforcement() }
        jobHeartbeat = escopo.launch { lacoDeHeartbeat() }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
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
            // aparelho volta a depender do código de instalação, digitado pelo técnico.
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

                val resposta = backend.enviarHeartbeat(localizacao)

                resposta.comandosPendentes.forEach { processarComando(it) }

                // Estado desejado do servidor: recupera o bloqueio se o sinal local se perdeu
                // (app reinstalado, dados limpos) mesmo sem um comando novo.
                sincronizarBloqueio(resposta.deveEstarBloqueado)

                // Auto-atualizacao OTA: se o servidor anunciou um APK mais novo, baixa, confere
                // o hash e se instala. Roda por ultimo — bloqueio antes de tudo.
                resposta.atualizacaoAgente?.let { atualizador.processar(it) }
            }.onFailure { Log.w(TAG, "Heartbeat falhou; nova tentativa no proximo ciclo.", it) }

            delay(AgenteConfig.INTERVALO_HEARTBEAT_SEGUNDOS * 1000)
        }
    }

    private var localizacaoNegadaReportada = false

    /** Reporta uma unica vez por execucao: nao faz sentido inundar o painel a cada 2 minutos. */
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
        runCatching {
            val resposta = backend.enviarHeartbeat(localizacao = null)
            resposta.comandosPendentes.forEach { processarComando(it) }
            sincronizarBloqueio(resposta.deveEstarBloqueado)
        }.onFailure { Log.w(TAG, "Falha ao buscar pendências após reconectar.", it) }
    }

    // --- Execucao de comandos ---

    private suspend fun processarComando(comando: ComandoPendente) {
        if (comando.id.isBlank()) return

        try {
            // Aceita texto ("Bloquear") e número ("0"): o heartbeat e o SignalR usam
            // serializadores diferentes no backend, e um agente que só entendesse um dos formatos
            // descartaria o comando em silêncio se as duas pontas saíssem de sincronia.
            when (comando.tipoComando) {
                COMANDO_BLOQUEAR, COMANDO_BLOQUEAR_NUMERICO -> {
                    bloqueio.aplicar()
                    politica.definirQuiosque(ativo = true)
                }
                COMANDO_DESBLOQUEAR, COMANDO_DESBLOQUEAR_NUMERICO -> bloqueio.remover()
                else -> {
                    Log.w(TAG, "Comando desconhecido: ${comando.tipoComando}")
                    return
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
     */
    private suspend fun sincronizarBloqueio(deveEstarBloqueado: Boolean) {
        if (deveEstarBloqueado == bloqueio.ativo) return

        if (deveEstarBloqueado) {
            Log.w(TAG, "Servidor indica bloqueio ativo mas o sinal local se perdeu; reaplicando.")
            bloqueio.aplicar()
            politica.definirQuiosque(ativo = true)

            runCatching {
                backend.reportarAlerta(
                    TipoAlerta.BloqueioReaplicado,
                    "Bloqueio reaplicado a partir do estado do servidor (sinal local ausente).",
                )
            }
        } else {
            // O servidor nao considera o aparelho bloqueado: a TI ja liberou.
            bloqueio.remover()
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

    private fun iniciarEmPrimeiroPlano() {
        val abrirApp = PendingIntent.getActivity(
            this,
            0,
            Intent(this, EnrollmentActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )

        val notificacao: Notification = NotificationCompat.Builder(this, AgenteConfig.CANAL_NOTIFICACAO)
            .setContentTitle(getString(R.string.notificacao_titulo))
            .setContentText(getString(R.string.notificacao_texto))
            .setSmallIcon(R.drawable.ic_tracker)
            .setOngoing(true)
            .setContentIntent(abrirApp)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build()

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(
                AgenteConfig.ID_NOTIFICACAO_SERVICO,
                notificacao,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION,
            )
        } else {
            startForeground(AgenteConfig.ID_NOTIFICACAO_SERVICO, notificacao)
        }
    }

    companion object {
        private const val TAG = "AgenteService"
        private const val EVENTO_EXECUTAR_COMANDO = "ExecutarComando"
        private const val COMANDO_BLOQUEAR = "Bloquear"
        private const val COMANDO_DESBLOQUEAR = "Desbloquear"

        // Valores do enum TipoComandoRemoto quando serializado como número.
        private const val COMANDO_BLOQUEAR_NUMERICO = "0"
        private const val COMANDO_DESBLOQUEAR_NUMERICO = "1"

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
