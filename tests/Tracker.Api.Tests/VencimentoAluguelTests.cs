using System.Text.Json;
using Tracker.Api.Json;
using Tracker.Application.Dtos;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Protege o vencimento do aluguel contra o bug de fuso que já aconteceu: como <c>DateTime</c>, o
/// <see cref="UtcDateTimeConverter"/> (registrado para toda a API) carimbava a data com 'Z', e o
/// navegador em UTC-3 exibia o DIA ANTERIOR. O campo é <c>DateOnly</c> justamente por isso — data
/// civil não tem hora nem fuso. Se alguém trocar o tipo de volta, estes testes quebram.
/// </summary>
public class VencimentoAluguelTests
{
    /// <summary>Mesmas opções do Program.cs, para o teste valer sobre a serialização real da API.</summary>
    private static JsonSerializerOptions OpcoesDaApi()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opcoes.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        opcoes.Converters.Add(new UtcDateTimeConverter());
        return opcoes;
    }

    [Fact]
    public void Vencimento_SaiComoDataPura_SemHoraNemZ()
    {
        var request = new DefinirVencimentoAluguelRequest(new DateOnly(2026, 10, 5), null);

        var json = JsonSerializer.Serialize(request, OpcoesDaApi());

        Assert.Contains("\"2026-10-05\"", json);
        // Com hora/'Z', o front em UTC-3 voltaria um dia ao exibir.
        Assert.DoesNotContain("T00:00", json);
        Assert.DoesNotContain("Z\"", json);
    }

    [Fact]
    public void Vencimento_EhLidoDeDataPura()
    {
        var json = """{"vencimentoAluguel":"2026-10-05","motivo":null}""";

        var request = JsonSerializer.Deserialize<DefinirVencimentoAluguelRequest>(json, OpcoesDaApi());

        Assert.Equal(new DateOnly(2026, 10, 5), request!.VencimentoAluguel);
    }

    /// <summary>Data nula é ativo sem contrato — precisa atravessar o JSON como null, não como "0001-01-01".</summary>
    [Fact]
    public void SemVencimento_TrafegaComoNulo()
    {
        var request = new DefinirVencimentoAluguelRequest(null, "sem contrato");

        var json = JsonSerializer.Serialize(request, OpcoesDaApi());

        Assert.Contains("\"vencimentoAluguel\":null", json);
    }

    /// <summary>
    /// O DTO do dispositivo é o que alimenta a lista e o detalhe: a data tem que chegar lá do mesmo
    /// jeito, sem o conversor de UTC tocar nela.
    /// </summary>
    [Fact]
    public void DispositivoDto_LevaVencimentoComoDataPura()
    {
        var dto = new DispositivoDto(
            Guid.NewGuid(),
            Tracker.Domain.Enums.TipoDispositivo.Notebook,
            "SN-1",
            "PAT-1",
            "MAQ-0142",
            Tracker.Domain.Enums.StatusDispositivo.EmUso,
            new DateTime(2025, 1, 10),
            null, null, null, null, null, null, null, null, null, null, null,
            VencimentoAluguel: new DateOnly(2026, 1, 31));

        var json = JsonSerializer.Serialize(dto, OpcoesDaApi());

        Assert.Contains("\"vencimentoAluguel\":\"2026-01-31\"", json);
    }
}
