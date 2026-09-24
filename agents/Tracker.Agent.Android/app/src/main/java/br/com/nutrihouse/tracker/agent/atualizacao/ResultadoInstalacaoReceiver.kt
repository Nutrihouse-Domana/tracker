package br.com.nutrihouse.tracker.agent.atualizacao

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.pm.PackageInstaller
import android.util.Log

/**
 * Recebe o desfecho da sessao de instalacao do PackageInstaller.
 *
 * Tres finais possiveis:
 * - **PENDING_USER_ACTION** (sem Device Owner): o sistema pede confirmacao — abrimos o dialogo.
 *   Se o usuario nunca responder, o flag e liberado pelo timeout de 10 min do AtualizadorAgente,
 *   que tambem abandona a sessao pendurada antes da proxima tentativa.
 *   A permissao de sobreposicao (que o agente ja exige) isenta o app da restricao de abrir
 *   activity a partir do segundo plano no Android 10+.
 * - **SUCCESS**: o processo sera reiniciado pelo sistema; o BootReceiver (MY_PACKAGE_REPLACED)
 *   religa o servico. Nada a fazer aqui.
 * - **Falha**: registra e libera o flag para o proximo heartbeat tentar de novo.
 */
class ResultadoInstalacaoReceiver : BroadcastReceiver() {

    override fun onReceive(context: Context, intent: Intent) {
        val status = intent.getIntExtra(PackageInstaller.EXTRA_STATUS, PackageInstaller.STATUS_FAILURE)

        when (status) {
            PackageInstaller.STATUS_PENDING_USER_ACTION -> {
                @Suppress("DEPRECATION")
                val confirmacao = intent.getParcelableExtra<Intent>(Intent.EXTRA_INTENT)
                if (confirmacao == null) {
                    Log.w(TAG, "Sistema pediu confirmacao mas nao enviou a intent do dialogo.")
                    AtualizadorAgente.liberar()
                    return
                }

                confirmacao.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                runCatching { context.startActivity(confirmacao) }
                    .onFailure {
                        Log.w(TAG, "Nao foi possivel abrir o dialogo de confirmacao.", it)
                        AtualizadorAgente.liberar()
                    }
            }

            PackageInstaller.STATUS_SUCCESS ->
                Log.i(TAG, "Atualizacao instalada; o sistema vai reiniciar o agente.")

            else -> {
                val detalhe = intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE)
                Log.w(TAG, "Instalacao falhou (status $status): $detalhe")
                AtualizadorAgente.liberar()
            }
        }
    }

    companion object {
        private const val TAG = "ResultadoInstalacao"
        const val ACAO_RESULTADO = "br.com.nutrihouse.tracker.agent.RESULTADO_INSTALACAO"
    }
}
