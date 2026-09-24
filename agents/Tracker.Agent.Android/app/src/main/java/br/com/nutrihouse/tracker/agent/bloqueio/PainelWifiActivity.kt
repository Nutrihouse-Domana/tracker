package br.com.nutrihouse.tracker.agent.bloqueio

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import br.com.nutrihouse.tracker.agent.dados.Armazenamento

/**
 * Tela invisivel que "hospeda" o painel de Wi-Fi do sistema enquanto o aparelho esta bloqueado.
 *
 * Por que existe: sem Device Owner o app nao conecta sozinho a uma rede (Android 10+), entao a
 * tela de bloqueio abre o painel de Wi-Fi do sistema — e para o usuario conseguir toca-lo, a
 * sobreposicao precisa sair da frente. Antes ela saia por 60s fixos, e nesse tempo o usuario
 * apertava Home e usava o aparelho livremente.
 *
 * O painel (`Settings.Panel`) e uma janela translucida: a Activity que o abriu fica PAUSADA e
 * visivel por baixo (conferido no aparelho). Aproveitamos isso para saber, na hora, quando o
 * usuario deixa o painel:
 * - **onResume** de novo = o painel fechou (Concluido, toque fora, Voltar);
 * - **onStop** = o usuario saiu para outro lugar (Home, Recentes, "Mais configuracoes",
 *   notificacao, tela apagada).
 * Nos dois casos a sobreposicao volta imediatamente. O prazo de [Bloqueio] continua como teto.
 */
class PainelWifiActivity : Activity() {

    /** True depois que o painel foi pedido: o 1o onResume e o da propria abertura desta tela. */
    private var painelAberto = false

    /** Evita reaplicar duas vezes (onResume e onStop podem chegar em sequencia). */
    private var encerrado = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Desbloqueado nesse meio-tempo (ou aberta por engano): nao ha o que hospedar.
        if (!Armazenamento(this).bloqueado) {
            finish()
            return
        }
        // Recriada pelo sistema (ex.: processo morto): nao reabre o painel, so devolve a trava.
        if (savedInstanceState != null) encerrar("tela recriada")
    }

    override fun onResume() {
        super.onResume()
        if (encerrado) return

        if (!painelAberto) {
            painelAberto = true
            // Aberto a partir DESTA Activity (sem NEW_TASK): o painel fica na mesma tarefa, por
            // cima dela, e e isso que faz o ciclo de vida acima refletir o que o usuario faz.
            if (!WifiConnect.abrirPainelDoSistema(this)) encerrar("painel nao abriu")
            return
        }

        encerrar("painel fechado")
    }

    override fun onStop() {
        super.onStop()
        if (painelAberto && !isChangingConfigurations) encerrar("usuario saiu do painel")
    }

    private fun encerrar(motivo: String) {
        if (encerrado) return
        encerrado = true
        Log.i(TAG, "Fim da pausa para Wi-Fi ($motivo): religando a trava.")
        Bloqueio(this).retomarAposWifi()
        finish()
    }

    companion object {
        private const val TAG = "PainelWifi"

        /** Abre o painel de Wi-Fi hospedado. Chamado pela sobreposicao de bloqueio. */
        fun abrir(context: Context): Boolean {
            val intent = Intent(context, PainelWifiActivity::class.java).addFlags(
                Intent.FLAG_ACTIVITY_NEW_TASK or
                    Intent.FLAG_ACTIVITY_NO_ANIMATION or
                    Intent.FLAG_ACTIVITY_EXCLUDE_FROM_RECENTS,
            )
            return runCatching { context.startActivity(intent); true }
                .onFailure { Log.w(TAG, "Falha ao abrir o painel de Wi-Fi hospedado.", it) }
                .getOrDefault(false)
        }
    }
}
