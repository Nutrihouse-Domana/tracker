namespace Tracker.Application.Dtos;

public record EstatisticasDashboardDto(
    int TotalNotebooks,
    int TotalTelefones,
    int TotalPerifericos,
    int EmUso,
    int EmEstoque,
    int EmManutencao,
    int Bloqueados,
    int Baixados);
