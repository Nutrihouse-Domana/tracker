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
    string? UsuarioAtualNome,
    string? UnidadeNome = null,
    // Quem responde pelo ativo: o usuário nominal ou, se for do local, a unidade.
    string? ResponsavelNome = null,
    // Rótulo manual da TI; o site exibe identificacaoExibida (rótulo → hostname → série).
    string? IdentificacaoInterna = null);

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
