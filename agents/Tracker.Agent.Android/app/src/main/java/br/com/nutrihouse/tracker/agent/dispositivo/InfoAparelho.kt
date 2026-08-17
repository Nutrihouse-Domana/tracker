package br.com.nutrihouse.tracker.agent.dispositivo

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.os.BatteryManager
import android.os.Build
import android.provider.Settings
import android.telephony.TelephonyManager
import android.util.Log

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

    /** True quando o app tem READ_PHONE_STATE concedida (so util ate o Android 9). */
    private val temPermissaoTelefonia: Boolean
        get() = context.checkSelfPermission(Manifest.permission.READ_PHONE_STATE) ==
            PackageManager.PERMISSION_GRANTED

    /**
     * True quando o aparelho tem modem celular. Tablet so Wi-Fi nao tem, e nele o IMEI nao existe
     * em lugar nenhum — nao adianta pedir permissao nem provisionar Device Owner.
     */
    val temModemCelular: Boolean
        get() = context.packageManager.hasSystemFeature(PackageManager.FEATURE_TELEPHONY)

    /**
     * Numero de serie de fabrica, quando o Android permite le-lo. Null quando nao permite.
     *
     * O que cada versao libera:
     * - Ate o Android 7.1 (API < 26): Build.SERIAL, sem permissao nenhuma.
     * - Android 8.0 a 9 (API 26-28): Build.getSerial(), exige READ_PHONE_STATE.
     * - Android 10+ (API 29+): **so com Device Owner**. Para app comum e proibido, e nao existe
     *   permissao que resolva — a unica saida e provisionar o aparelho como Device Owner.
     */
    @get:SuppressLint("HardwareIds", "MissingPermission")
    val numeroSerieReal: String?
        get() {
            val serie = runCatching {
                @Suppress("DEPRECATION")
                when {
                    Build.VERSION.SDK_INT < Build.VERSION_CODES.O -> Build.SERIAL
                    politica.ehDeviceOwner -> Build.getSerial()
                    Build.VERSION.SDK_INT <= Build.VERSION_CODES.P && temPermissaoTelefonia -> Build.getSerial()
                    else -> null
                }
            }.onFailure { Log.d(TAG, "Serial indisponivel: ${it.message}") }.getOrNull()

            return serie?.takeIf { it.isNotBlank() && it != Build.UNKNOWN }
        }

    /**
     * Identificador do aparelho enviado ao inventario: o serial de fabrica quando disponivel, e o
     * ANDROID_ID como ultimo recurso.
     *
     * ATENCAO ao provisorio: o ANDROID_ID e escopado por chave de assinatura do APK (Android 8+),
     * entao **muda quando o app e reinstalado assinado com outra chave**. Nao serve para
     * reencontrar o ativo depois de uma troca de agente — quem faz esse papel e o patrimonio.
     */
    @get:SuppressLint("HardwareIds")
    val numeroSerie: String
        get() {
            numeroSerieReal?.let { return it }

            val androidId = runCatching {
                Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID)
            }.getOrNull()

            // Prefixado para deixar explicito no inventario que nao e o serial de fabrica.
            return androidId?.takeIf { it.isNotBlank() }?.let { "ANDROID-$it" }
                ?: "ANDROID-DESCONHECIDO"
        }

    /**
     * IMEI. Existe apenas em aparelho com modem e, do Android 10 em diante, so e legivel com
     * Device Owner; entre o 8.0 e o 9 basta READ_PHONE_STATE.
     */
    @get:SuppressLint("HardwareIds", "MissingPermission")
    val imei: String?
        get() {
            if (!temModemCelular) return null
            if (!politica.ehDeviceOwner && !temPermissaoTelefonia) return null
            // Android 10+ recusa a leitura a app comum, com ou sem permissao concedida.
            if (Build.VERSION.SDK_INT > Build.VERSION_CODES.P && !politica.ehDeviceOwner) return null

            return runCatching {
                val telefonia = context.getSystemService(Context.TELEPHONY_SERVICE) as TelephonyManager
                @Suppress("DEPRECATION")
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) telefonia.imei else telefonia.deviceId
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
