package br.com.nutrihouse.tracker.agent.bloqueio

import android.annotation.SuppressLint
import android.content.Context
import android.content.Intent
import android.net.wifi.WifiConfiguration
import android.net.wifi.WifiManager
import android.os.Build
import android.provider.Settings
import android.util.Log
import br.com.nutrihouse.tracker.agent.dispositivo.Politica

/** Resultado de uma tentativa de conexao, para a tela mostrar o motivo ao usuario. */
data class ResultadoWifi(val ok: Boolean, val mensagem: String)

/**
 * Conexao a Wi-Fi a partir da tela de bloqueio — mesmo proposito do WifiConnect do agente
 * Windows: deixar o aparelho voltar a ficar online para receber o comando de Desbloquear da TI,
 * sem sair da trava.
 *
 * O Android impoe uma diferenca importante em relacao ao Windows: **desde o Android 10 um app
 * comum nao pode mais conectar a uma rede informando SSID e senha** (`addNetwork` passou a
 * retornar -1). Sobram dois caminhos, e a tela usa o que estiver disponivel:
 *
 * - **Com Device Owner:** a restricao nao se aplica. Conectamos direto, igual ao Windows.
 * - **Sem Device Owner:** abrimos o painel de Wi-Fi do sistema, que e uma tela restrita **apenas
 *   a Wi-Fi** — nao dá acesso ao resto das Configuracoes, entao o usuario nao consegue usá-la
 *   para desativar o agente.
 */
object WifiConnect {

    private const val TAG = "WifiConnect"

    private fun wifi(context: Context) =
        context.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager

    /** True quando conseguimos conectar sozinhos, sem mandar o usuario para o sistema. */
    fun podeConectarDireto(context: Context): Boolean = Politica(context).ehDeviceOwner

    /**
     * Redes ao alcance, mais fortes primeiro. Exige permissao de localizacao (o Android trata a
     * lista de redes como dado de posicao) — ela ja e pedida no registro do aparelho.
     */
    @SuppressLint("MissingPermission")
    fun listarRedes(context: Context): List<String> = runCatching {
        val gerenciador = wifi(context)
        if (!gerenciador.isWifiEnabled) gerenciador.isWifiEnabled = true

        @Suppress("DEPRECATION")
        gerenciador.startScan()

        gerenciador.scanResults
            .filter { it.SSID.isNotBlank() }
            .sortedByDescending { it.level }
            .map { it.SSID }
            .distinct()
    }.onFailure { Log.w(TAG, "Falha ao listar redes.", it) }.getOrDefault(emptyList())

    /**
     * Conecta a rede. So funciona com Device Owner; sem ele, quem chama deve usar
     * [abrirPainelDoSistema].
     */
    @SuppressLint("MissingPermission")
    fun conectar(context: Context, ssid: String, senha: String): ResultadoWifi {
        if (ssid.isBlank()) return ResultadoWifi(false, "Selecione uma rede.")

        if (!podeConectarDireto(context)) {
            return ResultadoWifi(false, "Use o botão \"Abrir Wi-Fi do sistema\" neste aparelho.")
        }

        return runCatching {
            val gerenciador = wifi(context)
            if (!gerenciador.isWifiEnabled) gerenciador.isWifiEnabled = true

            // WifiConfiguration é obsoleto desde a API 29, mas continua sendo o único caminho que
            // conecta de imediato — e para Device Owner ele segue autorizado. A alternativa
            // moderna (WifiNetworkSuggestion) apenas sugere a rede e depende de o sistema decidir
            // quando usá-la, o que não serve para destravar um aparelho agora.
            @Suppress("DEPRECATION")
            val config = WifiConfiguration().apply {
                SSID = "\"$ssid\""
                if (senha.isBlank()) {
                    allowedKeyManagement.set(WifiConfiguration.KeyMgmt.NONE)
                } else {
                    preSharedKey = "\"$senha\""
                }
            }

            @Suppress("DEPRECATION")
            val id = gerenciador.addNetwork(config)
            if (id == -1) return ResultadoWifi(false, "O sistema recusou a configuração da rede.")

            @Suppress("DEPRECATION")
            val ligou = gerenciador.enableNetwork(id, true)

            if (ligou) ResultadoWifi(true, "Conectando… aguarde alguns segundos.")
            else ResultadoWifi(false, "Não foi possível ativar a rede. Confira a senha.")
        }.onFailure { Log.w(TAG, "Falha ao conectar.", it) }
            .getOrElse { ResultadoWifi(false, "Erro ao conectar: ${it.message}") }
    }

    /**
     * Abre o painel de Wi-Fi do sistema. A partir do Android 10 usamos `Settings.Panel`, que
     * mostra **somente** a lista de redes sobre o app atual — o usuario nao chega ao resto das
     * Configuracoes por ali. Em versoes anteriores nao existe painel restrito e caimos nas
     * Configuracoes de Wi-Fi.
     */
    fun abrirPainelDoSistema(context: Context): Boolean {
        val intent = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            Intent(Settings.Panel.ACTION_WIFI)
        } else {
            Intent(Settings.ACTION_WIFI_SETTINGS)
        }.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)

        return runCatching { context.startActivity(intent); true }
            .onFailure { Log.w(TAG, "Falha ao abrir o painel de Wi-Fi.", it) }
            .getOrDefault(false)
    }
}
