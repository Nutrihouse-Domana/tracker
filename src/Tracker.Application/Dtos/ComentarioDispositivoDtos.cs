namespace Tracker.Application.Dtos;

public record ComentarioDispositivoDto(
    Guid Id,
    Guid DispositivoId,
    string Texto,
    Guid CriadoPorAdmId,
    string CriadoPorAdmNome,
    DateTime CriadoEm);

// Autoria vem do ADM logado no servidor, não do corpo.
public record CriarComentarioRequest(string Texto);
