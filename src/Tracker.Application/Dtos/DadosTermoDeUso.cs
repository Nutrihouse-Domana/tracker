namespace Tracker.Application.Dtos;

/// <summary>Dados já resolvidos (nomes, não apenas IDs) para preencher o PDF do termo de uso.</summary>
/// <remarks>
/// <c>DataGeracao</c> é um instante em UTC (como tudo que o sistema grava); o gerador do PDF é quem
/// o converte para o horário de Brasília ao imprimir (<c>FusoBrasil.ParaBrasilia</c>).
/// </remarks>
public record DadosTermoDeUso(
    string VersaoTermo,
    string NomeUsuario,
    string CpfUsuario,
    string EmailUsuario,
    string TelefoneUsuario,
    string TipoDispositivo,
    string NumeroSerie,
    string? AssetTag,
    string? Hostname,
    string EstadoDispositivo,
    DateTime DataGeracao);
