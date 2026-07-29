namespace Tracker.Application.Dtos;

/// <summary>Dados já resolvidos (nomes, não apenas IDs) para preencher o PDF do termo de uso.</summary>
public record DadosTermoDeUso(
    string VersaoTermo,
    string NomeUsuario,
    string CpfUsuarioMascarado,
    string EmailUsuario,
    string TipoDispositivo,
    string NumeroSerie,
    string AssetTag,
    string? Hostname,
    DateTime DataGeracao);
