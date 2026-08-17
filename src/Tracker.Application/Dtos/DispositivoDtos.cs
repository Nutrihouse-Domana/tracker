using Tracker.Domain.Enums;

namespace Tracker.Application.Dtos;

public record DispositivoDto(
    Guid Id,
    TipoDispositivo Tipo,
    string NumeroSerie,
    string? AssetTag,
    StatusDispositivo Status,
    DateTime DataAquisicao,
    string? Observacoes,
    Guid? UsuarioAtualId,
    string? UsuarioAtualNome,
    string? Hostname,
    string? SistemaOperacional,
    string? VersaoSO,
    string? Imei,
    DateTime? UltimoHeartbeatEm,
    bool? GeofenceAtivo,
    string? AgenteVersao,
    TipoPeriferico? TipoPeriferico,
    string? Fabricante = null,
    string? Modelo = null,
    int? BateriaPercentual = null,
    NivelProtecaoAgente? NivelProtecao = null,
    Guid? UnidadeId = null,
    string? UnidadeNome = null,
    // False = ativo do local: lotado na unidade, sem detentor individual.
    bool PertenceAUsuario = true,
    // Quem responde pelo ativo: o usuário nominal ou, se for do local, a unidade.
    string? ResponsavelNome = null,
    DateTime? ExcluidoEm = null,
    string? MotivoExclusao = null);

/// <summary>
/// Remove o dispositivo do inventário. Exige a senha do ADM logado.
/// <para><c>Definitiva = false</c> (padrão): arquiva — o registro e o histórico continuam no banco
/// e o ativo pode ser restaurado.</para>
/// <para><c>Definitiva = true</c>: APAGA a linha e, em cascata, movimentações, reparos, comentários,
/// alertas, localizações, comandos e os termos de uso assinados (inclusive os PDFs em disco).
/// Não há volta.</para>
/// </summary>
public record ExcluirDispositivoRequest(string Senha, string? Motivo, bool Definitiva = false);

/// <summary>
/// Define se o dispositivo pertence a um usuário nominal ou ao local (unidade).
/// Exige a senha do ADM logado: a troca solta o detentor atual e muda a responsabilidade do ativo.
/// </summary>
public record DefinirPosseDispositivoRequest(
    bool PertenceAUsuario,
    Guid? UnidadeId,
    string Senha,
    string? Motivo);

public record DefinirUnidadeDispositivoRequest(Guid? UnidadeId);

public record CriarNotebookRequest(
    string NumeroSerie,
    string AssetTag,
    string Hostname,
    string SistemaOperacional,
    string? VersaoSO,
    string? MacAddress,
    DateTime DataAquisicao,
    string? Observacoes,
    Guid? UnidadeId = null,
    bool PertenceAUsuario = true);

/// <summary>Celular ou tablet. IMEI é opcional porque tablets só-Wi-Fi não têm.</summary>
public record CriarMobileRequest(
    string NumeroSerie,
    string AssetTag,
    string Hostname,
    string SistemaOperacional,
    string? Imei,
    string? MacAddress,
    DateTime DataAquisicao,
    string? Observacoes,
    Guid? UnidadeId = null,
    bool PertenceAUsuario = true);

public record CriarPerifericoRequest(
    string NumeroSerie,
    TipoPeriferico TipoPeriferico,
    DateTime DataAquisicao,
    string? Observacoes,
    Guid? UnidadeId = null,
    bool PertenceAUsuario = true);

public record AtualizarStatusDispositivoRequest(StatusDispositivo Status);

public record AtribuirDispositivoRequest(
    Guid UsuarioId,
    Guid? LocalDestinoId,
    string? Motivo,
    Guid RegistradoPorAdmId);
