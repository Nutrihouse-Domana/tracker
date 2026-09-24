package br.com.nutrihouse.tracker.agent.servico

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.util.Log
import br.com.nutrihouse.tracker.agent.bloqueio.Bloqueio
import br.com.nutrihouse.tracker.agent.dados.Armazenamento

/**
 * Religa o agente depois de reiniciar o aparelho ou de atualizar o APK.
 *
 * Reaplica o bloqueio aqui mesmo, antes de o servico subir: e o que impede a janela de alguns
 * segundos em que o usuario poderia usar um aparelho bloqueado logo apos ligar.
 */
class BootReceiver : BroadcastReceiver() {

    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action !in ACOES_TRATADAS) return

        val armazenamento = Armazenamento(context)

        // O bloqueio depende SO do sinal persistido, nunca do token: um 401 apaga o token, e se a
        // trava exigisse "registrado", o aparelho bloqueado voltaria livre no primeiro reboot.
        val bloqueado = armazenamento.bloqueado
        if (bloqueado) {
            runCatching { Bloqueio(context).garantirAtivo() }
                .onFailure { Log.w(TAG, "Falha ao reaplicar o bloqueio no boot.", it) }
        }

        // Sem token e sem bloqueio nao ha o que o servico fazer: quem o sobe e a tela de registro.
        if (!armazenamento.registrado && !bloqueado) return

        Log.i(TAG, "Religando o agente apos ${intent.action}.")
        AgenteService.iniciar(context)
    }

    private companion object {
        const val TAG = "BootReceiver"

        val ACOES_TRATADAS = setOf(
            Intent.ACTION_BOOT_COMPLETED,
            Intent.ACTION_MY_PACKAGE_REPLACED,
        )
    }
}
