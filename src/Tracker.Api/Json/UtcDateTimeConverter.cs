using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tracker.Api.Json;

/// <summary>
/// Garante que todo DateTime trafegue como UTC ISO-8601 com o sufixo 'Z'.
/// Os timestamps são gravados em UTC (DateTime.UtcNow), mas o SQL Server os devolve com
/// Kind=Unspecified — sem esse conversor o JSON sai sem 'Z' e o navegador interpreta como
/// hora local, exibindo o horário 3h adiantado (Brasília é UTC-3).
/// Registrado também para DateTime? automaticamente pelo System.Text.Json.
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var valor = reader.GetDateTime();
        // Datas vindas do banco chegam sem fuso: assume-se UTC (é como foram gravadas).
        return valor.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(valor, DateTimeKind.Utc)
            : valor.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
    }
}
