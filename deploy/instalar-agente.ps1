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

# Executa um .exe com o preference em 'Continue' (stderr de exe nativo com "Stop" vira erro
# terminante quando a saida esta redirecionada) e devolve o $LASTEXITCODE para checagem.
# Mesmo padrao do preparar-ambiente-dev.ps1.
function ExecutarExterno {
    param([scriptblock]$Bloco)
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Bloco | Out-Null } finally { $ErrorActionPreference = $anterior }
    return $LASTEXITCODE
}

# sc.exe query devolve 1060 quando o servico nao existe (codigo, nao texto: a saida e traduzida).
function ServicoExiste {
    return ((ExecutarExterno { sc.exe query $NomeServico }) -ne 1060)
}

# ACL explicita, sem heranca: SYSTEM e Administradores com controle total; Usuarios so com o
# direito informado. SIDs, nao nomes: no Windows em pt-BR "Administrators" nao existe.
# Dono = Administradores e filhos resetados para herdar SO da pasta: um arquivo plantado antes
# (quando C:\ herdava "Modificar" para usuarios autenticados) perde as permissoes proprias.
function ProtegerPasta {
    param([string]$Pasta, [string]$DireitoUsuarios)
    New-Item -ItemType Directory -Force $Pasta | Out-Null
    $c = ExecutarExterno { icacls.exe $Pasta /setowner '*S-1-5-32-544' /T /C /Q }
    if ($c -ne 0) { throw "icacls /setowner falhou em '$Pasta' (codigo $c)." }
    $c = ExecutarExterno { icacls.exe $Pasta /reset /T /C /Q }
    if ($c -ne 0) { throw "icacls /reset falhou em '$Pasta' (codigo $c)." }
    $c = ExecutarExterno {
        icacls.exe $Pasta /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "*S-1-5-32-545:(OI)(CI)$DireitoUsuarios" /Q
    }
    if ($c -ne 0) { throw "icacls /grant falhou em '$Pasta' (codigo $c)." }
}

# Remove instalacao anterior, se houver, e ESPERA o servico sumir: sem a espera o sc create
# seguinte falha com 1072 (marcado para exclusao) ou 1073 (ja existe).
if (ServicoExiste) {
    Write-Host "Removendo servico existente..."
    $c = ExecutarExterno { sc.exe stop $NomeServico }
    # 1062 = o servico nao estava em execucao. Outro erro e so aviso: quem decide e o delete.
    if ($c -ne 0 -and $c -ne 1062) { Write-Warning "sc stop retornou $c; tentando remover mesmo assim." }
    $c = ExecutarExterno { sc.exe delete $NomeServico }
    # 1072 = ja marcado para exclusao: basta esperar sumir.
    if ($c -ne 0 -and $c -ne 1072) { throw "sc delete falhou (codigo $c). Veja a observacao sobre SDDL abaixo." }

    $limite = (Get-Date).AddSeconds(30)
    while ((ServicoExiste) -and ((Get-Date) -lt $limite)) { Start-Sleep -Milliseconds 500 }
    if (ServicoExiste) {
        throw "O servico '$NomeServico' ainda aparece registrado 30s apos o delete. Feche services.msc e o Gerenciador de Tarefas, ou reinicie a maquina, e rode de novo."
    }
}

# Permissoes das pastas (antes do servico subir, para o agente ja nascer protegido).
# C:\TrackerAgent: usuarios so Leitura e Execucao (a tarefa do overlay roda como o usuario e
# precisa executar o .exe). %ProgramData%\TrackerAgent (locked.flag): usuarios so leitura -
# o overlay nao grava nada la (a deteccao de tela viva e por processo, ver BloqueioTela.cs).
Write-Host "Aplicando permissoes em $CaminhoInstalacao e na pasta de dados..."
ProtegerPasta -Pasta $CaminhoInstalacao -DireitoUsuarios "RX"
$pastaDados = Join-Path $env:ProgramData "TrackerAgent"
ProtegerPasta -Pasta $pastaDados -DireitoUsuarios "R"
# Marcador antigo, gravado pelo usuario (substituido pela deteccao por processo).
Remove-Item (Join-Path $pastaDados "overlay-alive.flag") -Force -ErrorAction SilentlyContinue

# Cria o servico como LocalSystem, inicio automatico.
Write-Host "Criando servico $NomeServico..."
$c = ExecutarExterno { sc.exe create $NomeServico binPath= "`"$exe`"" start= auto obj= "LocalSystem" DisplayName= "Tracker Agent Nutrihouse" }
if ($c -ne 0) { throw "sc create falhou (codigo $c)." }
$c = ExecutarExterno { sc.exe description $NomeServico "Agente de rastreabilidade de ativos (heartbeat, localizacao e bloqueio remoto)." }
if ($c -ne 0) { Write-Warning "sc description retornou $c." }

# Reinicio automatico em caso de falha.
$c = ExecutarExterno { sc.exe failure $NomeServico reset= 86400 actions= restart/5000/restart/5000/restart/5000 }
if ($c -ne 0) { Write-Warning "sc failure retornou $c (reinicio automatico em falha nao configurado)." }

# Protecao contra parada/remocao: nega STOP/DELETE a usuarios nao-administradores.
# SDDL: concede controle total a SYSTEM (SY) e Administradores (BA); usuarios comuns so leem status.
#
# NAO reduza o conjunto de direitos de SY/BA. A versao anterior usava "CCLCSWRPWPDTLOCRRC",
# que omite SD (delete), DC (mudar configuracao) e WD (mudar a propria DACL): o servico ficava
# impossivel de remover ou reconfigurar ATE POR ADMINISTRADOR, e a reinstalacao morria com
# "sc create" 1073 (servico ja existe). Para consertar uma maquina nesse estado, apague
# HKLM\SYSTEM\CurrentControlSet\Services\TrackerAgent\Security e reinstale.
$direitosTotais = "CCDCLCSWRPWPDTLOCRSDRCWDWO"
$sddl = "D:(A;;$direitosTotais;;;SY)(A;;$direitosTotais;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)"
$c = ExecutarExterno { sc.exe sdset $NomeServico $sddl }
if ($c -ne 0) { Write-Warning "sc sdset retornou $c - usuario comum pode conseguir parar o servico." }

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
# telas e evitada pelo proprio agente (processo do overlay por sessao + mutex no overlay).
$settingsLock = New-ScheduledTaskSettingsSet -MultipleInstances Parallel -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName "TrackerAgentLock" -Action $acaoLock -Principal $principalLock -Trigger $triggerLock -Settings $settingsLock -Force | Out-Null

Write-Host "Iniciando servico..."
$c = ExecutarExterno { sc.exe start $NomeServico }
if ($c -ne 0) { throw "sc start falhou (codigo $c). Veja $CaminhoInstalacao\agent.log." }

Write-Host "Concluido. Servico '$NomeServico' instalado e em execucao."
Write-Host "Observacao: um administrador local do Windows ainda consegue remover o servico - a protecao impede apenas usuarios comuns."
