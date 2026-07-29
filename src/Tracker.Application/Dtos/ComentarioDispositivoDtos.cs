namespace Tracker.Application.Dtos;

public record ComentarioDispositivoDto(
    Guid Id,
    Guid DispositivoId,
    string Texto,
    Guid CriadoPorAdmId,
    string CriadoPorAdmNome,
    DateTime CriadoEm);

public record CriarComentarioRequest(string Texto, Guid CriadoPorAdmId);
