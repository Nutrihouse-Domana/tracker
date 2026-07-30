namespace Tracker.Application.Dtos;

/// <summary>Dados já resolvidos (nomes, não apenas IDs) para preencher o PDF do termo de uso.</summary>
public record DadosTermoDeUso(
    string VersaoTermo,
    string NomeUsuario,
    string CpfUsuario,
    string EmailUsuario,
    string TelefoneUsuario,
    string TipoDispositivo,
    string NumeroSerie,
    string AssetTag,
    string? Hostname,
    string EstadoDispositivo,
    DateTime DataGeracao);
