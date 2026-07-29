<#
.SYNOPSIS
  Instala o Tracker Agent como Servico do Windows, rodando como LocalSystem, com
  inicio automatico no boot e protecao contra parada/remocao por usuario comum.

.DESCRIPTION
  Execute como Administrador na maquina de destino (VM de teste ou notebook corporativo).
  Antes de instalar:
    1. Publique o agente:  dotnet publish agents/Tracker.Agent.Windows -c Release -r win-x64 --self-contained false -o C:\TrackerAgent
    2. Edite C:\TrackerAgent\appsettings.json com a URL do backend e a EnrollmentKey (ou AgentToken).
       Para valer o bloqueio de verdade, defina "Simular": false.

.PARAMETER CaminhoInstalacao
  Pasta onde o agente publicado esta (contem Tracker.Agent.Windows.exe).

.PARAMETER NomeServico
  Nome do servico do Windows. Padrao: TrackerAgent.
#>
param(
    [string]$CaminhoInstalacao = "C:\TrackerAgent",
    [string]$NomeServico = "TrackerAgent"
)

$ErrorActionPreference = "Stop"

# Exige privilegios de administrador.
$admin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { throw "Execute este script como Administrador." }

$exe = Join-Path $CaminhoInstalacao "Tracker.Agent.Windows.exe"
if (-not (Test-Path $exe)) { throw "Executavel nao encontrado: $exe. Publique o agente primeiro (ver .DESCRIPTION)." }

# Remove instalacao anterior, se houver.
if (Get-Service -Name $NomeServico -ErrorAction SilentlyContinue) {
    Write-Host "Removendo servico existente..."
    sc.exe stop $NomeServico | Out-Null
    Start-Sleep -Seconds 2
    sc.exe delete $NomeServico | Out-Null
    Start-Sleep -Seconds 2
}

# Cria o servico como LocalSystem, inicio automatico.
Write-Host "Criando servico $NomeServico..."
sc.exe create $NomeServico binPath= "`"$exe`"" start= auto obj= "LocalSystem" DisplayName= "Tracker Agent Nutrihouse" | Out-Null
sc.exe description $NomeServico "Agente de rastreabilidade de ativos (heartbeat, localizacao e bloqueio remoto)." | Out-Null

# Reinicio automatico em caso de falha.
sc.exe failure $NomeServico reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

# Protecao contra parada/remocao: nega STOP/DELETE a usuarios nao-administradores.
# SDDL: concede controle total a SYSTEM (SY) e Administradores (BA); usuarios comuns so leem status.
$sddl = "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCLCSWRPWPDTLOCRRC;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)"
sc.exe sdset $NomeServico $sddl | Out-Null

# Tarefa agendada que lanca a TELA DE BLOQUEIO na sessao interativa do usuario.
# O servico (sessao 0) nao mostra UI na tela do usuario; ele dispara esta tarefa, que roda como
# INTERACTIVE (S-1-5-4) executando o proprio agente em modo --overlay.
Write-Host "Criando tarefa de bloqueio (TrackerAgentLock)..."
$acaoLock = New-ScheduledTaskAction -Execute $exe -Argument "--overlay"
$principalLock = New-ScheduledTaskPrincipal -GroupId "S-1-5-4" -RunLevel Limited
# Gatilho "ao fazer logon": se o dispositivo estiver bloqueado (locked.flag existe), a tela
# aparece imediatamente no login, sem brecha. O overlay se auto-encerra se nao houver bloqueio.
$triggerLock = New-ScheduledTaskTrigger -AtLogOn
# MultipleInstances=Parallel: NAO ignora novos disparos (essencial para re-bloquear depois de
# um desbloqueio). Sem limite de tempo de execucao (a tela pode ficar horas). A duplicacao de
# telas e evitada pelo proprio agente (marcador overlay-alive).
$settingsLock = New-ScheduledTaskSettingsSet -MultipleInstances Parallel -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName "TrackerAgentLock" -Action $acaoLock -Principal $principalLock -Trigger $triggerLock -Settings $settingsLock -Force | Out-Null

Write-Host "Iniciando servico..."
sc.exe start $NomeServico | Out-Null

Write-Host "Concluido. Servico '$NomeServico' instalado e em execucao."
Write-Host "Observacao: um administrador local do Windows ainda consegue remover o servico - a protecao impede apenas usuarios comuns."
