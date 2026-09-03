namespace Tracker.Domain.Enums;

public enum TipoDispositivo
{
    Notebook,
    /// <summary>Celulares e tablets: mesmo agente, mesmo tratamento no inventário.</summary>
    Mobile,
    Periferico
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
    ProtecaoRebaixada
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
    DispositivoPosseAlterada,
    DispositivoArquivado,
    DispositivoExcluidoDefinitivo,
    DispositivoRestaurado,

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
