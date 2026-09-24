namespace Tracker.Application.Common;

/// <summary>
/// Fuso de Brasília num lugar só. O banco guarda timestamps em UTC; o que precisa de fuso é
/// (a) o que é IMPRESSO para pessoas (PDF do termo) e (b) filtro por data civil vinda da tela
/// (auditoria "de/até"): o dia 10/01 em Brasília vai de 10/01 03:00Z a 11/01 03:00Z.
/// </summary>
public static class FusoBrasil
{
    /// <summary>Id do fuso no Windows (dev e produção) e no padrão IANA (Linux/macOS).</summary>
    private static readonly string[] IdsDoFuso = ["E. South America Standard Time", "America/Sao_Paulo"];

    private static readonly Lazy<TimeZoneInfo> FusoLazy = new(ResolverFuso);

    /// <summary>Fuso de Brasília. Sem o fuso no sistema, cai para UTC-3 fixo (o Brasil não tem horário de verão desde 2019).</summary>
    public static TimeZoneInfo Fuso => FusoLazy.Value;

    private static TimeZoneInfo ResolverFuso()
    {
        foreach (var id in IdsDoFuso)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Brasilia", TimeSpan.FromHours(-3), "Brasília", "Brasília");
    }

    /// <summary>
    /// Converte um instante UTC para o horário de Brasília. <c>Kind=Unspecified</c> é tratado como
    /// UTC — é como o SQL Server devolve os timestamps que gravamos com <c>DateTime.UtcNow</c>.
    /// </summary>
    public static DateTime ParaBrasilia(DateTime instanteUtc)
    {
        var utc = instanteUtc.Kind switch
        {
            DateTimeKind.Utc => instanteUtc,
            DateTimeKind.Local => instanteUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc),
        };
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Fuso);
    }

    /// <summary>00:00 do dia civil em Brasília, expresso em UTC (limite inferior inclusivo).</summary>
    public static DateTime InicioDoDiaEmUtc(DateOnly dia) =>
        TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Fuso);

    /// <summary>
    /// Fim do dia civil em Brasília, expresso em UTC como o INÍCIO do dia seguinte — use com
    /// comparação "menor que" (limite superior exclusivo), sem perder o último milissegundo.
    /// </summary>
    public static DateTime FimDoDiaEmUtc(DateOnly dia) => InicioDoDiaEmUtc(dia.AddDays(1));
}
