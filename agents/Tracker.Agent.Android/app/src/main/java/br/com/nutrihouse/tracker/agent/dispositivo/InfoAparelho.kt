package br.com.nutrihouse.tracker.agent.dispositivo

import android.annotation.SuppressLint
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.BatteryManager
import android.os.Build
import android.provider.Settings
import android.telephony.TelephonyManager
import android.util.Log
import br.com.nutrihouse.tracker.agent.dados.Armazenamento

/** Tipo enviado ao backend no auto-cadastro; espelha Tracker.Domain.Enums.TipoDispositivo. */
enum class TipoAparelho { Mobile }

/**
 * Le a identificacao e a telemetria do aparelho.
 *
 * Sobre identificadores: desde o Android 10 o numero de serie e o IMEI exigem privilegio de
 * sistema. Com Device Owner o agente consegue os dois; sem ele, cai para o ANDROID_ID, que e
 * estavel por aparelho+app e so muda em restauracao de fabrica. Isso e suficiente como chave de
 * inventario e e o que o backend recebe como NumeroSerie.
 */
class InfoAparelho(private val context: Context) {

    private val politica by lazy { Politica(context) }

    // lazy: abrir o armazenamento criptografado passa pelo Keystore do aparelho, caro demais
    // para refazer a cada leitura de propriedade.
    private val armazenamento by lazy { Armazenamento(context) }

    /**
     * Celular e tablet entram no inventario como o MESMO tipo (Mobile): o agente e identico nos
     * dois, e separa-los por tamanho de tela nao mudava nada no controle de ativos.
     */
    val tipo: TipoAparelho get() = TipoAparelho.Mobile

    val fabricante: String get() = Build.MANUFACTURER.orEmpty()
    val modelo: String get() = Build.MODEL.orEmpty()
    val versaoSO: String get() = "Android ${Build.VERSION.RELEASE} (API ${Build.VERSION.SDK_INT})"
    val sistemaOperacional: String get() = "Android"

    /** Nome que o usuario deu ao aparelho; cai para fabricante + modelo quando indisponivel. */
    val hostname: String
        get() {
            val nomeEscolhido = runCatching {
                Settings.Global.getString(context.contentResolver, Settings.Global.DEVICE_NAME)
            }.getOrNull()

            return nomeEscolhido?.takeIf { it.isNotBlank() } ?: "$fabricante $modelo".trim()
        }

    /**
     * Identificador estavel do aparelho. Tenta o numero de serie real (so funciona com Device
     * Owner) e cai para o ANDROID_ID.
     */
    /**
     * Identidade do aparelho no inventario. Prioriza a serie digitada pelo tecnico no registro:
     * ela vem da etiqueta, e diferente do ANDROID_ID nao muda quando a assinatura do app muda —
     * o que ja causou registros duplicados.
     */
    val numeroSerieInventario: String
        get() = armazenamento.numeroSerie?.takeIf { it.isNotBlank() } ?: numeroSerie

    // @get:: a propriedade nao tem backing field (so getter), entao a anotacao vai no getter.
    @get:SuppressLint("HardwareIds")
    val numeroSerie: String
        get() {
            if (politica.ehDeviceOwner) {
                val serie = runCatching { Build.getSerial() }.getOrNull()
                if (!serie.isNullOrBlank() && serie != Build.UNKNOWN) return serie
            }

            val androidId = runCatching {
                Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID)
            }.getOrNull()

            // Prefixado para deixar explicito no inventario que nao e o serial de fabrica.
            return androidId?.takeIf { it.isNotBlank() }?.let { "ANDROID-$it" }
                ?: "ANDROID-DESCONHECIDO"
        }

    /** IMEI, disponivel apenas com Device Owner e em aparelhos com modem celular. */
    @get:SuppressLint("HardwareIds", "MissingPermission")
    val imei: String?
        get() {
            if (!politica.ehDeviceOwner) return null

            return runCatching {
                val telefonia = context.getSystemService(Context.TELEPHONY_SERVICE) as TelephonyManager
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) telefonia.imei else null
            }.onFailure { Log.d(TAG, "IMEI indisponivel: ${it.message}") }.getOrNull()
                ?.takeIf { it.isNotBlank() }
        }

    /** Carga da bateria em porcentagem, ou null se o sistema nao informar. */
    val bateriaPercentual: Int?
        get() = runCatching {
            val gerenciador = context.getSystemService(Context.BATTERY_SERVICE) as BatteryManager
            val nivel = gerenciador.getIntProperty(BatteryManager.BATTERY_PROPERTY_CAPACITY)
            if (nivel in 0..100) return nivel

            // Alguns aparelhos nao respondem a BATTERY_PROPERTY_CAPACITY; le do broadcast fixo.
            val status: Intent? = context.registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
            val bruto = status?.getIntExtra(BatteryManager.EXTRA_LEVEL, -1) ?: -1
            val escala = status?.getIntExtra(BatteryManager.EXTRA_SCALE, -1) ?: -1
            if (bruto >= 0 && escala > 0) bruto * 100 / escala else null
        }.getOrNull()

    private companion object {
        const val TAG = "InfoAparelho"
    }
}
