namespace Tracker.Application.Dtos;

public record GerarTermoDeUsoRequest(
    Guid DispositivoId,
    Guid UsuarioId,
    string VersaoTermo);

public record TermoDeUsoDto(
    Guid Id,
    Guid DispositivoId,
    Guid UsuarioId,
    string UsuarioNome,
    string VersaoTermo,
    DateTime DataAceite,
    string HashDocumento,
    string CaminhoArquivoAnexado,
    bool AssinadoAnexado);
