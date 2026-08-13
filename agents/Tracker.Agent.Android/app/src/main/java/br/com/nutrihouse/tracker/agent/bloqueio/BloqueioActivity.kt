package br.com.nutrihouse.tracker.agent.bloqueio

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.Build
import android.os.Bundle
import android.view.KeyEvent
import android.view.WindowManager
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import br.com.nutrihouse.tracker.agent.dados.Armazenamento
import br.com.nutrihouse.tracker.agent.databinding.ActivityBloqueioBinding
import br.com.nutrihouse.tracker.agent.dispositivo.InfoAparelho
import br.com.nutrihouse.tracker.agent.dispositivo.Politica

/**
 * Tela cheia de bloqueio. Equivale ao LockOverlay do agente Windows: informa que o aparelho e da
 * Nutrihouse e como liberar o uso.
 *
 * Ela nao pretende ser inviolavel sozinha — no Android o botao Home nao pode ser interceptado por
 * um app comum. O que sustenta a trava e o ciclo de reaplicacao do AgenteService (lockNow a cada
 * poucos segundos); com Device Owner, o modo quiosque fecha essa brecha de vez.
 */
class BloqueioActivity : AppCompatActivity() {

    private lateinit var binding: ActivityBloqueioBinding
    private val politica by lazy { Politica(this) }
    private val armazenamento by lazy { Armazenamento(this) }

    /** Fecha a tela quando a TI desbloqueia o aparelho pelo painel. */
    private val receptorFechar = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) = finish()
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityBloqueioBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // Aparece mesmo com a tela de bloqueio do sistema por cima e acende o display.
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O_MR1) {
            setShowWhenLocked(true)
            setTurnScreenOn(true)
        } else {
            @Suppress("DEPRECATION")
            window.addFlags(
                WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED or
                    WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON,
            )
        }
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)

        esconderBarrasDoSistema()
        preencherIdentificacao()

        ContextCompat.registerReceiver(
            this,
            receptorFechar,
            IntentFilter(Bloqueio.ACAO_FECHAR_TELA),
            ContextCompat.RECEIVER_NOT_EXPORTED,
        )
    }

    override fun onResume() {
        super.onResume()

        // Se a TI desbloqueou enquanto a tela estava aberta, sai sozinha.
        if (!armazenamento.bloqueado) {
            finish()
            return
        }

        // Modo quiosque: so o Device Owner consegue: impede troca de app, Home e Recentes.
        if (politica.ehDeviceOwner) {
            runCatching { startLockTask() }
        }
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        // A gaveta de notificacoes reexibe as barras; reesconde ao voltar o foco.
        if (hasFocus) esconderBarrasDoSistema()
    }

    /** Consome Voltar, Recentes e teclas de energia/volume que dariam saida da tela. */
    override fun onKeyDown(keyCode: Int, event: KeyEvent?): Boolean = when (keyCode) {
        KeyEvent.KEYCODE_BACK,
        KeyEvent.KEYCODE_APP_SWITCH,
        KeyEvent.KEYCODE_MENU,
        -> true
        else -> super.onKeyDown(keyCode, event)
    }

    @Deprecated("Necessario para compatibilidade abaixo do Android 13.")
    @Suppress("DEPRECATION", "MissingSuperCall")
    override fun onBackPressed() {
        // Sem chamada ao super: Voltar nao pode sair da tela de bloqueio.
    }

    override fun onUserLeaveHint() {
        super.onUserLeaveHint()
        // Sem Device Owner o Home nao pode ser bloqueado; o servico reabre a tela em seguida.
    }

    override fun onDestroy() {
        runCatching { unregisterReceiver(receptorFechar) }
        if (politica.ehDeviceOwner) runCatching { stopLockTask() }
        super.onDestroy()
    }

    private fun esconderBarrasDoSistema() {
        WindowCompat.setDecorFitsSystemWindows(window, false)
        WindowInsetsControllerCompat(window, binding.root).apply {
            hide(WindowInsetsCompat.Type.systemBars())
            systemBarsBehavior = WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        }
    }

    private fun preencherIdentificacao() {
        val info = InfoAparelho(this)
        val patrimonio = armazenamento.assetTag

        binding.textoIdentificacao.text = buildString {
            if (!patrimonio.isNullOrBlank()) appendLine("Patrimônio: $patrimonio")
            appendLine("Série: ${info.numeroSerie}")
            append("${info.fabricante} ${info.modelo}")
        }
    }
}
