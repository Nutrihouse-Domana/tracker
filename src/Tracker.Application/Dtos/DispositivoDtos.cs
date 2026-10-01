using Tracker.Domain.Enums;

namespace Tracker.Application.Dtos;

public record DispositivoDto(
    Guid Id,
    TipoDispositivo Tipo,
    string NumeroSerie,
    string? AssetTag,
    // Apelido/rótulo definido pela TI. Quando presente, é a identificação exibida do aparelho.
    string? IdentificacaoInterna,
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
    string? MotivoExclusao = null,
    // Vencimento do contrato de aluguel. Nulo nos ativos próprios (sem contrato registrado).
    DateOnly? VencimentoAluguel = null,
    // --- Impressora (nulos nos demais tipos) ---
    TipoImpressora? TipoImpressora = null,
    bool? Colorida = null,
    // Interruptor de aluguel da impressora: nela o aluguel NÃO segue o prefixo "MAQ".
    bool? Alugada = null,
    ConexaoImpressora? Conexao = null,
    string? EnderecoRede = null,
    // MAC: da impressora ou reportado pelo agente (notebook/mobile).
    string? MacAddress = null,
    bool? MonitorarSnmp = null,
    // Último estado lido por SNMP (Fase 2). Nulos enquanto a impressora nunca foi lida.
    bool? Online = null,
    DateTime? UltimaLeituraEm = null,
    long? ContadorPaginas = null);

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

/// <summary>
/// Define (ou limpa, enviando nulo) a data de vencimento do contrato de aluguel do equipamento.
/// Diferente da identificação interna e da posse, NÃO pede a senha do ADM: é um dado operacional
/// do contrato, não uma ação que muda a responsabilidade sobre o ativo.
/// </summary>
public record DefinirVencimentoAluguelRequest(DateOnly? VencimentoAluguel, string? Motivo);

/// <summary>
/// Define (ou limpa) a identificação interna do dispositivo — um rótulo próprio da TI, à parte do
/// hostname reportado pelo agente. Exige a senha do ADM logado: passa a ser o nome exibido do ativo.
/// <c>IdentificacaoInterna = null/vazio</c> volta a exibir o hostname/número de série.
/// </summary>
public record DefinirIdentificacaoInternaRequest(
    string? IdentificacaoInterna,
    string Senha,
    string? Motivo);

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

/// <summary>
/// Impressora. Não tem agente e é sempre do LOCAL: não há <c>PertenceAUsuario</c> no request e a
/// unidade é obrigatória. Patrimônio é opcional (impressora alugada costuma não ter etiqueta).
/// O aluguel é o interruptor <see cref="Alugada"/>, não o prefixo "MAQ" da identificação.
/// </summary>
public record CriarImpressoraRequest(
    string NumeroSerie,
    string? AssetTag,
    TipoImpressora TipoImpressora,
    string? Fabricante,
    string? Modelo,
    bool Colorida,
    bool Alugada,
    ConexaoImpressora Conexao,
    string? EnderecoRede,
    string? MacAddress,
    bool MonitorarSnmp,
    DateTime DataAquisicao,
    string? Observacoes,
    Guid? UnidadeId,
    DateOnly? VencimentoAluguel = null);

/// <summary>
/// Liga/desliga o interruptor de aluguel da impressora. Não pede a senha do ADM (dado do contrato,
/// como o vencimento), mas é auditado.
/// </summary>
public record DefinirAluguelImpressoraRequest(bool Alugada, string? Motivo = null);

/// <summary>
/// Conexão da impressora. Com <c>Usb</c> o endereço é descartado e o SNMP fica desligado (o
/// servidor não alcança impressora ligada a um computador). <c>MonitorarSnmp = true</c> exige
/// <c>Rede</c> com endereço preenchido.
/// </summary>
public record DefinirConexaoImpressoraRequest(
    ConexaoImpressora Conexao,
    string? EnderecoRede,
    string? MacAddress,
    bool MonitorarSnmp);

/// <summary>Dados descritivos da impressora (corrige o que foi digitado no cadastro).</summary>
public record DefinirDadosImpressoraRequest(
    TipoImpressora TipoImpressora,
    string? Fabricante,
    string? Modelo,
    bool Colorida);

public record AtualizarStatusDispositivoRequest(StatusDispositivo Status);

// A autoria (quem registrou) vem do ADM logado no servidor, não do corpo — por isso não há
// RegistradoPorAdmId aqui.
public record AtribuirDispositivoRequest(
    Guid UsuarioId,
    Guid? LocalDestinoId,
    string? Motivo);
