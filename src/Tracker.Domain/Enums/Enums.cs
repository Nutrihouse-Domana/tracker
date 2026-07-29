namespace Tracker.Domain.Enums;

public enum TipoDispositivo
{
    Notebook,
    Telefone,
    Periferico
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

public enum StatusReparo
{
    Aberto,
    EmAndamento,
    Concluido,
    Cancelado
}
