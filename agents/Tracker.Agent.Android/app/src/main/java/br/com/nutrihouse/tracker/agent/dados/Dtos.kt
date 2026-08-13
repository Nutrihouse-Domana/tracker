package br.com.nutrihouse.tracker.agent.dados

/**
 * Comando entregue pelo backend. Os nomes dos campos precisam bater com ComandoPendenteDto do
 * backend: o SignalR desserializa por reflexao usando Gson.
 */
data class ComandoPendente(
    val id: String = "",
    val tipoComando: String = "",
)

/** Resposta do heartbeat. deveEstarBloqueado e o estado desejado segundo o servidor. */
data class RespostaHeartbeat(
    val comandosPendentes: List<ComandoPendente>,
    val deveEstarBloqueado: Boolean,
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
