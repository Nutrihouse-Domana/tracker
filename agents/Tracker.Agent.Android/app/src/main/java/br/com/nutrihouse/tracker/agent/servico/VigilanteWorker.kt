package br.com.nutrihouse.tracker.agent.servico

import android.content.Context
import android.util.Log
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import br.com.nutrihouse.tracker.agent.bloqueio.Bloqueio
import br.com.nutrihouse.tracker.agent.dados.Armazenamento
import java.util.concurrent.TimeUnit

/**
 * Rede de seguranca do servico. Fabricantes com gestao agressiva de bateria (Xiaomi, Samsung,
 * Motorola) encerram servicos em primeiro plano; o WorkManager e o unico agendamento que o
 * sistema respeita de forma confiavel, e ele religa o agente.
 *
 * O intervalo minimo aceito pelo Android para trabalho periodico e 15 minutos.
 */
class VigilanteWorker(context: Context, params: WorkerParameters) : CoroutineWorker(context, params) {

    override suspend fun doWork(): Result {
        val armazenamento = Armazenamento(applicationContext)

        // O bloqueio vem primeiro e depende SO do sinal persistido — nunca do token, que um 401
        // apaga. Se o servico estava morto, o aparelho ficou usavel nesse meio.
        val bloqueado = armazenamento.bloqueado
        if (bloqueado) {
            runCatching { Bloqueio(applicationContext).garantirAtivo() }
                .onFailure { Log.w(TAG, "Falha ao reaplicar o bloqueio.", it) }
        }

        if (!armazenamento.registrado && !bloqueado) return Result.success()

        AgenteService.iniciar(applicationContext)
        return Result.success()
    }

    companion object {
        private const val TAG = "VigilanteWorker"
        private const val NOME = "tracker-vigilante"

        /** Agenda (ou mantem) a verificacao periodica. Seguro chamar a cada inicializacao do app. */
        fun agendar(context: Context) {
            val pedido = PeriodicWorkRequestBuilder<VigilanteWorker>(15, TimeUnit.MINUTES)
                .setConstraints(Constraints.Builder().build())
                .build()

            WorkManager.getInstance(context).enqueueUniquePeriodicWork(
                NOME,
                // KEEP: nao reinicia a contagem do periodo a cada abertura do app.
                ExistingPeriodicWorkPolicy.KEEP,
                pedido,
            )
        }
    }
}
