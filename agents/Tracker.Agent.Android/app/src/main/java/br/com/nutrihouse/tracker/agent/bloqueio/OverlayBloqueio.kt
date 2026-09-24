package br.com.nutrihouse.tracker.agent.bloqueio

import android.content.Context
import android.graphics.PixelFormat
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.provider.Settings
import android.util.Log
import android.view.Gravity
import android.view.KeyEvent
import android.view.LayoutInflater
import android.view.View
import android.view.WindowManager
import android.widget.ArrayAdapter
import androidx.core.view.isVisible
import br.com.nutrihouse.tracker.agent.R
import br.com.nutrihouse.tracker.agent.dados.Armazenamento
import br.com.nutrihouse.tracker.agent.databinding.ActivityBloqueioBinding
import br.com.nutrihouse.tracker.agent.dispositivo.InfoAparelho
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

/**
 * Tela de bloqueio como **janela de sobreposição**, não como Activity.
 *
 * A diferença importa: uma Activity pode ser deixada de lado com o botão Home, e o
 * `DevicePolicyManager.lockNow()` apenas aciona a trava de tela do sistema — o usuário digita a
 * senha dele e volta a usar o aparelho normalmente. Uma janela `TYPE_APPLICATION_OVERLAY` fica
 * acima de **qualquer** aplicativo, inclusive da tela inicial, e consome os toques: o aparelho
 * deixa de estar disponível, que é o efeito que a TI espera de um bloqueio remoto.
 *
 * Requisito: permissão "Sobrepor outros apps". Sem ela o Android recusa a janela — e é por isso
 * que o app insiste nessa permissão no registro.
 */
object OverlayBloqueio {

    @Volatile
    private var janela: View? = null

    private val mainHandler = Handler(Looper.getMainLooper())

    /** Trabalho assíncrono da tela (varredura de Wi-Fi), sempre devolvido na main thread. */
    private val escopoUi = CoroutineScope(SupervisorJob() + Dispatchers.Main)

    @Volatile
    private var varredura: Job? = null

    val visivel: Boolean get() = janela != null

    /**
     * Executa na thread principal. O laço de enforcement do AgenteService roda em Dispatchers.IO,
     * e o Android proíbe criar ou remover janelas fora da main thread — a tentativa falha ao
     * construir o Handler interno da View, e a sobreposição simplesmente nunca sobe.
     */
    private fun naThreadPrincipal(acao: () -> Unit) {
        if (Looper.myLooper() == Looper.getMainLooper()) acao() else mainHandler.post(acao)
    }

    /** Sobe a janela de bloqueio. Idempotente: chamar de novo com ela visível não faz nada. */
    fun mostrar(context: Context) = naThreadPrincipal { mostrarInterno(context) }

    private fun mostrarInterno(context: Context) {
        if (janela != null) return

        if (!Settings.canDrawOverlays(context)) {
            Log.e(TAG, "Sem permissão de sobreposição: não é possível deixar o aparelho indisponível.")
            return
        }

        val wm = context.getSystemService(WindowManager::class.java) ?: return
        val binding = ActivityBloqueioBinding.inflate(LayoutInflater.from(context))
        preencherIdentificacao(context, binding)
        ligarPainelWifi(context, binding)

        val raiz = binding.root.apply {
            // Consome todo toque que não caia num controle: nada por baixo recebe interação.
            setOnTouchListener { _, _ -> true }
            // Consome Voltar. Home e Recentes são do sistema e não podem ser interceptados sem
            // Device Owner — mas com a janela por cima, voltar à tela inicial não serve de nada.
            isFocusableInTouchMode = true
            setOnKeyListener { _, codigo, _ -> codigo == KeyEvent.KEYCODE_BACK }
        }

        val params = WindowManager.LayoutParams(
            WindowManager.LayoutParams.MATCH_PARENT,
            WindowManager.LayoutParams.MATCH_PARENT,
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY
            } else {
                @Suppress("DEPRECATION")
                WindowManager.LayoutParams.TYPE_SYSTEM_ALERT
            },
            // NO_LIMITS + IN_SCREEN cobrem também as áreas das barras de status e navegação.
            // SHOW_WHEN_LOCKED e TURN_SCREEN_ON fazem o aviso aparecer já ao acordar o aparelho.
            WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN or
                WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS or
                WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED or
                WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON or
                WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON,
            PixelFormat.OPAQUE,
        ).apply {
            gravity = Gravity.TOP or Gravity.START
        }

        runCatching { wm.addView(raiz, params) }
            .onSuccess {
                janela = raiz
                raiz.requestFocus()
                Log.i(TAG, "Aparelho indisponível: janela de bloqueio ativa.")
            }
            .onFailure { Log.e(TAG, "Falha ao subir a janela de bloqueio.", it) }
    }

    /** Remove a janela, devolvendo o aparelho ao uso. Só a TI chega aqui, pelo Desbloquear. */
    fun esconder(context: Context) = naThreadPrincipal { esconderInterno(context) }

    private fun esconderInterno(context: Context) {
        val atual = janela ?: return
        val wm = context.getSystemService(WindowManager::class.java)

        // A varredura em andamento escreveria numa janela que não existe mais.
        varredura?.cancel()
        varredura = null

        runCatching { wm?.removeView(atual) }
            .onFailure { Log.w(TAG, "Falha ao remover a janela de bloqueio.", it) }

        janela = null
        Log.i(TAG, "Janela de bloqueio removida.")
    }

    /**
     * Wi-Fi na tela de bloqueio: sem rede, o aparelho nunca receberia o comando de Desbloquear.
     * Mesmo propósito do painel do agente Windows.
     */
    private fun ligarPainelWifi(context: Context, binding: ActivityBloqueioBinding) {
        val podeConectarDireto = WifiConnect.podeConectarDireto(context)

        // Sem Device Owner o app não consegue conectar sozinho (restrição do Android 10+):
        // sobra o painel do sistema, então os controles próprios só atrapalhariam.
        binding.listaRedes.isVisible = podeConectarDireto
        binding.campoSenhaWifi.isVisible = podeConectarDireto
        binding.botaoAtualizarRedes.isVisible = podeConectarDireto
        binding.botaoConectarWifi.isVisible = podeConectarDireto
        binding.botaoPainelSistema.isVisible = !podeConectarDireto

        binding.botaoWifi.setOnClickListener {
            val abrindo = !binding.painelWifi.isVisible
            binding.painelWifi.isVisible = abrindo
            if (abrindo && podeConectarDireto) atualizarRedes(context, binding)
            if (abrindo && !podeConectarDireto) {
                binding.textoStatusWifi.setText(R.string.bloqueio_wifi_sem_permissao)
            }
        }

        binding.botaoAtualizarRedes.setOnClickListener { atualizarRedes(context, binding) }

        binding.botaoConectarWifi.setOnClickListener {
            val ssid = binding.listaRedes.selectedItem?.toString().orEmpty()
            val senha = binding.campoSenhaWifi.text?.toString().orEmpty()
            val resultado = WifiConnect.conectar(context, ssid, senha)
            binding.textoStatusWifi.text = resultado.mensagem
            if (resultado.ok) binding.campoSenhaWifi.text = null
        }

        binding.botaoPainelSistema.setOnClickListener {
            binding.textoStatusWifi.setText(R.string.bloqueio_wifi_sistema_aberto)
            // A sobreposição precisa sair da frente, senão o painel do sistema fica inacessível.
            Bloqueio(context).suspenderParaWifi()
            WifiConnect.abrirPainelDoSistema(context)
        }
    }

    /**
     * A varredura real leva alguns segundos: roda fora da main thread (senão a tela de bloqueio
     * congela) e o resultado volta para ela para atualizar a lista.
     */
    private fun atualizarRedes(context: Context, binding: ActivityBloqueioBinding) {
        if (varredura?.isActive == true) return

        binding.textoStatusWifi.setText(R.string.bloqueio_wifi_procurando)
        binding.botaoAtualizarRedes.isEnabled = false

        varredura = escopoUi.launch {
            // listarRedes troca para Dispatchers.IO sozinha; daqui em diante voltamos na main.
            val redes = WifiConnect.listarRedes(context)

            binding.listaRedes.adapter = ArrayAdapter(
                context,
                android.R.layout.simple_spinner_dropdown_item,
                redes,
            )
            binding.botaoAtualizarRedes.isEnabled = true

            binding.textoStatusWifi.text = if (redes.isEmpty()) {
                context.getString(R.string.bloqueio_wifi_nenhuma)
            } else {
                context.getString(R.string.bloqueio_wifi_encontradas, redes.size)
            }
        }
    }

    /** Patrimônio e série na tela: a TI identifica o aparelho sem precisar liberá-lo. */
    private fun preencherIdentificacao(context: Context, binding: ActivityBloqueioBinding) {
        binding.textoIdentificacao.text = textoIdentificacao(context)
    }

    /**
     * Texto de identificação usado pela sobreposição e pela BloqueioActivity. A série exibida é a
     * DIGITADA pelo técnico no registro — a mesma da etiqueta e do inventário. Sem ela (registros
     * antigos), cai para a de fábrica e, por fim, para o identificador provisório ANDROID-<id>.
     */
    fun textoIdentificacao(context: Context): String {
        val info = InfoAparelho(context)
        val armazenamento = Armazenamento(context)
        val patrimonio = armazenamento.assetTag
        val serie = armazenamento.numeroSerie?.takeIf { it.isNotBlank() }
            ?: info.numeroSerieReal
            ?: info.numeroSerie

        return buildString {
            if (!patrimonio.isNullOrBlank()) appendLine("Patrimônio: $patrimonio")
            appendLine("Série: $serie")
            append("${info.fabricante} ${info.modelo}")
        }
    }

    private const val TAG = "OverlayBloqueio"
}
