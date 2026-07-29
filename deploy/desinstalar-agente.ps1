<#
.SYNOPSIS
  Remove o servico do Tracker Agent. Execute como Administrador.
#>
param([string]$NomeServico = "TrackerAgent")

$ErrorActionPreference = "Stop"

$admin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { throw "Execute este script como Administrador." }

if (Get-Service -Name $NomeServico -ErrorAction SilentlyContinue) {
    sc.exe stop $NomeServico | Out-Null
    Start-Sleep -Seconds 2
    sc.exe delete $NomeServico | Out-Null
    Write-Host "Servico '$NomeServico' removido."
} else {
    Write-Host "Servico '$NomeServico' nao encontrado."
}

# Remove a tarefa de bloqueio, se existir.
Unregister-ScheduledTask -TaskName "TrackerAgentLock" -Confirm:$false -ErrorAction SilentlyContinue
Write-Host "Tarefa 'TrackerAgentLock' removida (se existia)."
