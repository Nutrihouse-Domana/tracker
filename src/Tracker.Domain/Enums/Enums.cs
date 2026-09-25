namespace Tracker.Domain.Enums;

public enum TipoDispositivo
{
    Notebook,
    /// <summary>Celulares e tablets: mesmo agente, mesmo tratamento no inventário.</summary>
    Mobile,
    Periferico,
    /// <summary>Impressora: sem agente; monitorada opcionalmente por SNMP pelo servidor.</summary>
    Impressora
    // Novos valores SEMPRE no fim: o número é gravado como discriminador TPH na tabela.
}

public enum StatusDispositivo
{
    EmEstoque,
    EmUso,
    EmManutencao,
    Bloqueado,
    Baixado
}

public enum TipoPeriferico
{
    Mouse,
    Teclado,
    Monitor,
    Dock,
    Headset,
    Outro
}

public enum OrigemLocalizacao
{
    GpsAndroid,
    IpGeolocationWindows,
    WifiConhecida,
    ManualTI
}

public enum AcaoAoSairDoGeofence
{
    BloquearAutomaticamente,
    ApenasAlertar
}

public enum TipoComandoRemoto
{
    Bloquear,
    Desbloquear
}

public enum StatusComandoRemoto
{
    Pendente,
    Enviado,
    Confirmado,
    Falhou
}

/// <summary>
/// Quão protegido está o agente contra remoção pelo próprio usuário. Só se aplica a Android:
/// no Windows o agente roda como Serviço e o campo fica nulo.
/// </summary>
public enum NivelProtecaoAgente
{
    /// <summary>App instalado sem permissão de administrador: usuário desinstala à vontade.</summary>
    Nenhuma,
    /// <summary>Device Admin ativo: o Android impede a desinstalação, mas o usuário pode desativar o admin.</summary>
    DeviceAdmin,
    /// <summary>Device Owner (provisionado por QR em aparelho zerado): desinstalação bloqueada de fato.</summary>
    DeviceOwner
}

/// <summary>Eventos que o agente reporta por conta própria, sem comando da TI.</summary>
public enum TipoAlertaDispositivo
{
    /// <summary>O usuário desativou (ou tentou desativar) a permissão de administrador do agente.</summary>
    AdminDesativado,
    /// <summary>A permissão de localização foi negada ou revogada.</summary>
    LocalizacaoNegada,
    /// <summary>O agente detectou o bloqueio ativo e o reaplicou após uma tentativa de contorno.</summary>
    BloqueioReaplicado,
    /// <summary>O agente perdeu privilégio de Device Owner/Admin e caiu para proteção menor.</summary>
    ProtecaoRebaixada,
    /// <summary>Impressora monitorada deixou de responder ao SNMP (gerado pelo servidor, não por agente).</summary>
    ImpressoraOffline,
    /// <summary>Toner/tinta/cilindro de impressora monitorada abaixo do limite (gerado pelo servidor).</summary>
    SuprimentoBaixo
    // Novos valores SEMPRE no fim (gravado como número).
}

public enum StatusReparo
{
    Aberto,
    EmAndamento,
    Concluido,
    Cancelado
}

/// <summary>
/// Tipo de alteração registrada na trilha de auditoria. Serializado como TEXTO na coluna Acao —
/// então a ordem é livre e novos valores podem entrar em qualquer lugar (ao contrário dos enums
/// usados como discriminador TPH). Um valor por tipo de ação de escrita da TI.
/// </summary>
public enum AcaoAuditoria
{
    // Dispositivo
    DispositivoCriado,
    DispositivoStatusAlterado,
    DispositivoAtribuido,
    DispositivoUnidadeDefinida,
    DispositivoIdentificacaoInternaDefinida,
    DispositivoVencimentoAluguelDefinido,
    DispositivoPosseAlterada,
    DispositivoArquivado,
    DispositivoExcluidoDefinitivo,
    DispositivoRestaurado,
    DispositivoAluguelDefinido,
    ImpressoraConexaoDefinida,

    // Reparo / comentário
    ReparoAberto,
    ReparoStatusAlterado,
    ComentarioAdicionado,

    // Agente / comandos
    ComandoBloquear,
    ComandoDesbloquear,
    EnrollmentGerado,
    AlertaReconhecido,
    ApkPublicado,

    // Termo de uso
    TermoRegistrado,

    // Cadastros
    DetentorCriado,
    DetentorEditado,
    DetentorDesativado,
    UnidadeCriada,
    UnidadeEditada,

    // Acessos (logins)
    AcessoCriado,
    AcessoEditado,
    AcessoAtivado,
    AcessoDesativado,
    AcessoPermissoesAlteradas,
    AcessoSenhaRedefinida,

    // Autenticação
    LoginEfetuado,
    LoginRecusado
}

/// <summary>Tecnologia/função da impressora, para filtro e relatório.</summary>
public enum TipoImpressora
{
    Laser,
    JatoDeTinta,
    Multifuncional,
    Termica,
    Matricial,
    Outra
    // Novos valores SEMPRE no fim.
}

/// <summary>
/// Como a impressora está ligada. Só <see cref="Rede"/> pode ser monitorada por SNMP: as
/// ligadas por USB a um computador não são alcançáveis pelo servidor.
/// </summary>
public enum ConexaoImpressora
{
    Rede,
    Usb
    // Novos valores SEMPRE no fim.
}
