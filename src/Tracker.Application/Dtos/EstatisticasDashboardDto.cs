namespace Tracker.Application.Dtos;

public record EstatisticasDashboardDto(
    int TotalNotebooks,
    int TotalMobiles,
    int TotalPerifericos,
    int TotalImpressoras,
    int EmUso,
    int EmEstoque,
    int EmManutencao,
    int Bloqueados,
    int Baixados);
