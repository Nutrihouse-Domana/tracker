package br.com.nutrihouse.tracker.agent.localizacao

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.location.Location
import android.os.SystemClock
import android.util.Log
import androidx.core.content.ContextCompat
import br.com.nutrihouse.tracker.agent.dados.Localizacao
import com.google.android.gms.location.CurrentLocationRequest
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
import kotlinx.coroutines.CancellableContinuation
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withTimeoutOrNull
import kotlin.coroutines.resume

/**
 * Localizacao do aparelho pelo provedor fundido do Google (GPS + Wi-Fi + rede).
 *
 * A ultima posicao conhecida vem primeiro porque e instantanea e nao liga o GPS — mas so se for
 * recente (ate [TOLERANCIA_IDADE_MS]); uma posicao de horas atras mandaria ao painel um lugar
 * onde o aparelho ja nao esta, e o heartbeat nao tem campo para a data da leitura. Sem posicao
 * recente pedimos uma leitura nova em prioridade equilibrada: precisao de rua basta para o
 * inventario, e ligar o GPS a cada heartbeat acabaria com a bateria.
 *
 * Tudo com prazo: [obter] nunca passa de [TIMEOUT_TOTAL_MS], para o heartbeat (que carrega os
 * comandos e o estado de bloqueio) nunca ficar preso esperando o GPS.
 */
class Geolocalizacao(private val context: Context) {

    private val cliente by lazy { LocationServices.getFusedLocationProviderClient(context) }

    val temPermissao: Boolean
        get() = ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) ==
            PackageManager.PERMISSION_GRANTED ||
            ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_COARSE_LOCATION) ==
            PackageManager.PERMISSION_GRANTED

    /** Retorna a posicao atual, ou null se a permissao faltar, o prazo estourar ou nao fixar. */
    suspend fun obter(): Localizacao? {
        if (!temPermissao) {
            Log.d(TAG, "Sem permissao de localizacao.")
            return null
        }

        return try {
            withTimeoutOrNull(TIMEOUT_TOTAL_MS) { ultimaConhecidaRecente() ?: leituraNova() }
                .also { if (it == null) Log.d(TAG, "Sem posicao recente dentro do prazo.") }
        } catch (e: SecurityException) {
            // Permissao revogada entre a checagem e a chamada.
            Log.w(TAG, "Permissao de localizacao revogada em tempo de execucao.", e)
            null
        } catch (e: CancellationException) {
            throw e // cancelamento do servico: propaga, nao e falha de localizacao
        } catch (e: Exception) {
            Log.w(TAG, "Falha ao obter localizacao.", e)
            null
        }
    }

    /** Ultima posicao do sistema, descartada se mais velha que [TOLERANCIA_IDADE_MS]. */
    @Suppress("MissingPermission")
    private suspend fun ultimaConhecidaRecente(): Localizacao? {
        val local = suspendCancellableCoroutine<Location?> { cont ->
            cliente.lastLocation
                .addOnSuccessListener { cont.retomar(it) }
                .addOnFailureListener { cont.retomar(null) }
                .addOnCanceledListener { cont.retomar(null) }
        } ?: return null

        val idadeMs = (SystemClock.elapsedRealtimeNanos() - local.elapsedRealtimeNanos) / 1_000_000
        if (idadeMs > TOLERANCIA_IDADE_MS) {
            Log.d(TAG, "Ultima posicao conhecida tem ${idadeMs / 1000}s; buscando leitura nova.")
            return null
        }
        return converter(local)
    }

    @Suppress("MissingPermission")
    private suspend fun leituraNova(): Localizacao? {
        val cancelamento = CancellationTokenSource()
        val pedido = CurrentLocationRequest.Builder()
            .setPriority(Priority.PRIORITY_BALANCED_POWER_ACCURACY)
            .setMaxUpdateAgeMillis(TOLERANCIA_IDADE_MS)
            .setDurationMillis(TIMEOUT_LEITURA_MS)
            .build()

        return suspendCancellableCoroutine<Location?> { cont ->
            // Timeout/cancelamento da coroutine tambem cancela o pedido no Play Services, para o
            // GPS nao continuar ligado sem ninguem esperando.
            cont.invokeOnCancellation { cancelamento.cancel() }

            cliente.getCurrentLocation(pedido, cancelamento.token)
                .addOnSuccessListener { cont.retomar(it) }
                .addOnFailureListener { cont.retomar(null) }
                .addOnCanceledListener { cont.retomar(null) }
        }?.let(::converter)
    }

    /** Retoma so uma vez e so se ainda houver quem espere (timeout ja pode ter cancelado). */
    private fun <T> CancellableContinuation<T?>.retomar(valor: T?) {
        if (isActive) resume(valor)
    }

    private fun converter(local: Location) = Localizacao(
        latitude = local.latitude,
        longitude = local.longitude,
        precisaoMetros = if (local.hasAccuracy()) local.accuracy.toDouble() else null,
    )

    private companion object {
        const val TAG = "Geolocalizacao"

        /** Aceita uma posicao de ate 5 minutos: o aparelho raramente cruza a cidade nesse tempo. */
        const val TOLERANCIA_IDADE_MS = 5 * 60 * 1000L

        /** Prazo da leitura nova no Play Services. */
        const val TIMEOUT_LEITURA_MS = 15_000L

        /** Prazo total de [obter]: ultima conhecida + leitura nova. Nunca segura o heartbeat. */
        const val TIMEOUT_TOTAL_MS = 20_000L
    }
}
