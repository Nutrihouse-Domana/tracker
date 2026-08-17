package br.com.nutrihouse.tracker.agent.dados

import android.content.Context
import android.util.Log
import br.com.nutrihouse.tracker.agent.AgenteConfig
import br.com.nutrihouse.tracker.agent.BuildConfig
import br.com.nutrihouse.tracker.agent.dispositivo.InfoAparelho
import br.com.nutrihouse.tracker.agent.dispositivo.Politica
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.util.concurrent.TimeUnit

/** Erro de comunicacao com o backend. Quem chama decide se tenta de novo no proximo ciclo. */
open class BackendException(mensagem: String) : Exception(mensagem)

/**
 * Codigo de instalacao recusado pelo backend. Separado do BackendException para a tela mostrar
 * "codigo incorreto" em vez de culpar a rede — sao acoes bem diferentes para o tecnico.
 */
class CodigoInvalidoException : BackendException("Código de instalação incorreto.")

/**
 * Comunicacao REST com o backend do Tracker. Os corpos sao montados a mao com org.json de
 * proposito: sao tres endpoints, e isso evita carregar uma biblioteca de serializacao e o
 * kotlin-reflect junto.
 */
class BackendClient(
    context: Context,
    private val armazenamento: Armazenamento,
) {

    private val appContext = context.applicationContext
    private val info = InfoAparelho(appContext)
    private val politica = Politica(appContext)

    private val http = OkHttpClient.Builder()
        .connectTimeout(20, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .build()

    private val baseUrl = BuildConfig.BACKEND_BASE_URL.trimEnd('/')

    /**
     * Auto-cadastro: cria o dispositivo no inventario (se ainda nao existir pelo numero de serie)
     * e recebe o token individual do aparelho. Guarda o token para as chamadas seguintes.
     *
     * O codigo de instalacao e usado apenas nesta chamada e **nao** e guardado no aparelho: se
     * ficasse salvo, um aparelho roubado carregaria a chave da empresa e poderia se recadastrar
     * sozinho depois de a TI revogar o token dele.
     */
    suspend fun registrar(
        codigoInstalacao: String,
        numeroSerie: String,
    ): Boolean = withContext(Dispatchers.IO) {
        val corpo = JSONObject().apply {
            put("hostname", info.hostname)
            // A serie digitada pelo tecnico e a identidade no inventario. Guardada localmente
            // para as telas do app, mas quem manda no registro e sempre o que foi digitado agora.
            put("numeroSerie", numeroSerie)
            put("sistemaOperacional", info.sistemaOperacional)
            put("versaoSO", info.versaoSO)
            put("macAddress", JSONObject.NULL) // Android 6+ nao expoe o MAC real ao app.
            put("tipo", info.tipo.name)
            put("imei", info.imei ?: JSONObject.NULL)
            put("fabricante", info.fabricante)
            put("modelo", info.modelo)
            put("nivelProtecao", politica.nivelProtecao.name)
        }

        val requisicao = Request.Builder()
            .url(baseUrl + AgenteConfig.ROTA_AUTO_ENROLLMENT)
            .header(AgenteConfig.HEADER_ENROLLMENT_KEY, codigoInstalacao)
            .post(corpo.toString().toRequestBody(JSON))
            .build()

        http.newCall(requisicao).execute().use { resposta ->
            val texto = resposta.body?.string().orEmpty()

            // O backend responde 401 quando a chave da empresa nao confere.
            if (resposta.code == 401) throw CodigoInvalidoException()

            if (!resposta.isSuccessful) {
                Log.w(TAG, "Auto-cadastro recusado (HTTP ${resposta.code}): $texto")
                throw BackendException(mensagemDeErro(resposta.code, texto))
            }

            val json = JSONObject(texto)
            armazenamento.numeroSerie = numeroSerie
            armazenamento.token = json.getString("token")
            armazenamento.dispositivoId = json.optString("dispositivoId").takeIf { it.isNotBlank() }
            armazenamento.assetTag = json.optString("assetTag").takeIf { it.isNotBlank() }
            true
        }
    }

    /** Telemetria periodica. Retorna os comandos a executar e o estado de bloqueio desejado. */
    suspend fun enviarHeartbeat(localizacao: Localizacao?): RespostaHeartbeat =
        withContext(Dispatchers.IO) {
            val corpo = JSONObject().apply {
                put("hostname", info.hostname)
                put("agenteVersao", AgenteConfig.VERSAO_AGENTE)
                put("latitude", localizacao?.latitude ?: JSONObject.NULL)
                put("longitude", localizacao?.longitude ?: JSONObject.NULL)
                put("enderecoAproximado", JSONObject.NULL) // Geocodificacao fica com o painel.
                put("precisaoMetros", localizacao?.precisaoMetros ?: JSONObject.NULL)
                put("bateriaPercentual", info.bateriaPercentual ?: JSONObject.NULL)
                put("nivelProtecao", politica.nivelProtecao.name)
                put("origem", "GpsAndroid")
            }

            val resposta = executarAutenticado(AgenteConfig.ROTA_HEARTBEAT, corpo)
            val json = JSONObject(resposta)

            val comandos = mutableListOf<ComandoPendente>()
            val array = json.optJSONArray("comandosPendentes")
            if (array != null) {
                for (i in 0 until array.length()) {
                    val item = array.getJSONObject(i)
                    comandos += ComandoPendente(
                        id = item.optString("id"),
                        tipoComando = item.optString("tipoComando"),
                    )
                }
            }

            RespostaHeartbeat(comandos, json.optBoolean("deveEstarBloqueado", false))
        }

    /** Confirma ao backend que o comando foi executado no aparelho. */
    suspend fun confirmarComando(comandoId: String) {
        withContext(Dispatchers.IO) {
            executarAutenticado("/api/agente/comandos/$comandoId/confirmar", corpo = null)
        }
    }

    /** Reporta um evento detectado pelo proprio agente (ex.: admin desativado pelo usuario). */
    suspend fun reportarAlerta(tipo: TipoAlerta, detalhe: String?) {
        withContext(Dispatchers.IO) {
            val corpo = JSONObject().apply {
                put("tipo", tipo.name)
                put("detalhe", detalhe ?: JSONObject.NULL)
            }
            executarAutenticado(AgenteConfig.ROTA_ALERTAS, corpo)
        }
    }

    private fun executarAutenticado(rota: String, corpo: JSONObject?): String {
        val token = armazenamento.token
            ?: throw BackendException("Aparelho ainda nao registrado (sem token).")

        val requisicao = Request.Builder()
            .url(baseUrl + rota)
            .header(AgenteConfig.HEADER_TOKEN, token)
            .post((corpo?.toString() ?: "{}").toRequestBody(JSON))
            .build()

        http.newCall(requisicao).execute().use { resposta ->
            val texto = resposta.body?.string().orEmpty()

            // 401 significa token revogado no painel: limpa o local para o agente se registrar de novo.
            if (resposta.code == 401) {
                armazenamento.token = null
                throw BackendException("Token do aparelho recusado; sera necessario registrar de novo.")
            }
            if (!resposta.isSuccessful) throw BackendException(mensagemDeErro(resposta.code, texto))

            return texto.ifBlank { "{}" }
        }
    }

    private fun mensagemDeErro(codigo: Int, corpo: String): String {
        val mensagem = runCatching { JSONObject(corpo).optString("message") }.getOrNull()
        return if (mensagem.isNullOrBlank()) "Falha na comunicacao (HTTP $codigo)." else mensagem
    }

    private companion object {
        const val TAG = "BackendClient"
        val JSON = "application/json; charset=utf-8".toMediaType()
    }
}
