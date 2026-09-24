using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tracker.Api.Json;

/// <summary>
/// Configuração de JSON da API num lugar só. Aplicada aos controllers E ao SignalR (que tem
/// serialização própria e NÃO herda a dos controllers) e reusada pelos testes — assim o teste
/// serializa exatamente como a API, em vez de uma cópia montada à mão que pode divergir.
/// </summary>
public static class JsonConfig
{
    /// <summary>Registra os conversores da API nas opções informadas.</summary>
    public static JsonSerializerOptions Aplicar(JsonSerializerOptions opcoes)
    {
        // Enums como texto: os agentes (Windows e Android) esperam "Bloquear", não 0 — um número
        // vindo do hub era descartado em silêncio.
        opcoes.Converters.Add(new JsonStringEnumConverter());
        // Emite todo DateTime como UTC com 'Z' (senão o front lê UTC como hora local: horário 3h
        // adiantado). DateOnly não passa por aqui — data civil trafega como "yyyy-MM-dd".
        opcoes.Converters.Add(new UtcDateTimeConverter());
        return opcoes;
    }
}
