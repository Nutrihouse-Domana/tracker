package br.com.nutrihouse.tracker.agent.dados

/**
 * Comando entregue pelo backend. Os nomes dos campos precisam bater com ComandoPendenteDto do
 * backend: o SignalR desserializa por reflexao usando Gson.
 */
data class ComandoPendente(
    val id: String = "",
    val tipoComando: String = "",
)

/**
 * Oferta de atualizacao OTA anunciada no heartbeat. O hash e conferido apos o download: um APK
 * corrompido (ou trocado no servidor entre o anuncio e o download) e descartado sem instalar.
 */
data class AtualizacaoAgente(
    val versaoCode: Int,
    val versaoNome: String,
    val hashSha256: String,
    val tamanhoBytes: Long,
)

/** Resposta do heartbeat. deveEstarBloqueado e o estado desejado segundo o servidor. */
data class RespostaHeartbeat(
    val comandosPendentes: List<ComandoPendente>,
    val deveEstarBloqueado: Boolean,
    val atualizacaoAgente: AtualizacaoAgente? = null,
)

/** Eventos que o agente reporta sozinho; espelha Tracker.Domain.Enums.TipoAlertaDispositivo. */
enum class TipoAlerta {
    AdminDesativado,
    LocalizacaoNegada,
    BloqueioReaplicado,
    ProtecaoRebaixada,
}

/** Coordenada capturada pelo aparelho. */
data class Localizacao(
    val latitude: Double,
    val longitude: Double,
    val precisaoMetros: Double?,
)
