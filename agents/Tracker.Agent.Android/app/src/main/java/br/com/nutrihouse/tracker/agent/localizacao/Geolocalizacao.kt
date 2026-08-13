package br.com.nutrihouse.tracker.agent.localizacao

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.util.Log
import androidx.core.content.ContextCompat
import br.com.nutrihouse.tracker.agent.dados.Localizacao
import com.google.android.gms.location.CurrentLocationRequest
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume

/**
 * Localizacao do aparelho pelo provedor fundido do Google (GPS + Wi-Fi + rede).
 *
 * A ultima posicao conhecida vem primeiro porque e instantanea e nao liga o GPS. Somente quando
 * ela nao existe pedimos uma leitura nova em prioridade equilibrada: precisao de rua e suficiente
 * para o inventario, e ligar o GPS a cada heartbeat acabaria com a bateria.
 */
class Geolocalizacao(private val context: Context) {

    private val cliente by lazy { LocationServices.getFusedLocationProviderClient(context) }

    val temPermissao: Boolean
        get() = ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) ==
            PackageManager.PERMISSION_GRANTED ||
            ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_COARSE_LOCATION) ==
            PackageManager.PERMISSION_GRANTED

    /** Retorna a posicao atual, ou null se a permissao faltar ou o aparelho nao conseguir fixar. */
    suspend fun obter(): Localizacao? {
        if (!temPermissao) {
            Log.d(TAG, "Sem permissao de localizacao.")
            return null
        }

        return try {
            ultimaConhecida() ?: leituraNova()
        } catch (e: SecurityException) {
            // Permissao revogada entre a checagem e a chamada.
            Log.w(TAG, "Permissao de localizacao revogada em tempo de execucao.", e)
            null
        } catch (e: Exception) {
            Log.w(TAG, "Falha ao obter localizacao.", e)
            null
        }
    }

    @Suppress("MissingPermission")
    private suspend fun ultimaConhecida(): Localizacao? = suspendCancellableCoroutine { cont ->
        cliente.lastLocation
            .addOnSuccessListener { local -> cont.resume(local?.let(::converter)) }
            .addOnFailureListener { cont.resume(null) }
    }

    @Suppress("MissingPermission")
    private suspend fun leituraNova(): Localizacao? = suspendCancellableCoroutine { cont ->
        val pedido = CurrentLocationRequest.Builder()
            .setPriority(Priority.PRIORITY_BALANCED_POWER_ACCURACY)
            .setMaxUpdateAgeMillis(TOLERANCIA_IDADE_MS)
            .setDurationMillis(TIMEOUT_MS)
            .build()

        cliente.getCurrentLocation(pedido, null)
            .addOnSuccessListener { local -> cont.resume(local?.let(::converter)) }
            .addOnFailureListener { cont.resume(null) }
    }

    private fun converter(local: android.location.Location) = Localizacao(
        latitude = local.latitude,
        longitude = local.longitude,
        precisaoMetros = if (local.hasAccuracy()) local.accuracy.toDouble() else null,
    )

    private companion object {
        const val TAG = "Geolocalizacao"

        /** Aceita uma posicao de ate 5 minutos: o aparelho raramente cruza a cidade nesse tempo. */
        const val TOLERANCIA_IDADE_MS = 5 * 60 * 1000L

        /** Desiste depois de 20s para nao segurar o ciclo de heartbeat dentro de um predio. */
        const val TIMEOUT_MS = 20_000L
    }
}
