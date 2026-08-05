using Tracker.Domain.Enums;

namespace Tracker.Application.Dtos;

/// <summary>Item de dispositivo no resultado da busca global.</summary>
public record BuscaDispositivoDto(
    Guid Id,
    TipoDispositivo Tipo,
    StatusDispositivo Status,
    string NumeroSerie,
    string? AssetTag,
    string? Hostname,
    string? Imei,
    string? UsuarioAtualNome);

/// <summary>Item de usuário no resultado da busca global.</summary>
public record BuscaUsuarioDto(
    Guid Id,
    string Nome,
    string Email,
    string Telefone,
    bool Ativo,
    int QuantidadeDispositivos);

/// <summary>Resultado da busca global do topo do site.</summary>
public record BuscaGlobalDto(
    string Termo,
    IEnumerable<BuscaDispositivoDto> Dispositivos,
    IEnumerable<BuscaUsuarioDto> Usuarios);
