<#
.SYNOPSIS
  Publica o instalador do agente Windows na pasta de rede lida pela GPO "Deploy Tracker Agent".

.DESCRIPTION
  Leva o Instalar-Agente.exe gerado pelo gerar-pendrive.ps1 para a pasta da GPO, com o nome
  versionado (Instalar-Agente-<versao>.exe), e por ULTIMO atualiza o versao.txt. O script da GPO
  (deploy\gpo\gpo_installer.cmd) compara esse versao.txt com o C:\TrackerAgent\versao.txt de cada
  maquina e so reinstala quando diferem.

  Por que nome versionado: o .exe da versao anterior pode estar em execucao numa maquina (boot
  em andamento), e um arquivo aberto na rede nao pode ser sobrescrito. Com um nome por versao a
  copia nunca briga com ninguem, e o versao.txt so aponta para o novo depois que ele chegou inteiro.

  Idempotente: se a pasta ja estiver nessa versao, nao faz nada. Rode a mao ou pelo CI
  (pasta padrao F:\AgentTracker no servidor do runner, ou a variavel TRACKER_AGENTE_GPO). Guia: docs\agentes\atualizacao-agente-gpo.md

.PARAMETER Origem
  Pasta com Instalar-Agente.exe e versao.txt (saida do gerar-pendrive.ps1). Padrao: C:\TrackerAgentDist

.PARAMETER PastaGpo
  Pasta lida pelas maquinas. No CI: F:\AgentTracker (compartilhada como \\192.168.0.254\AgentTracker).

.PARAMETER Manter
  Quantos instaladores versionados manter na pasta (o atual incluso). Padrao: 2.

.EXAMPLE
  .\deploy\publicar-agente-gpo.ps1 -PastaGpo F:\AgentTracker
#>
param(
    [string]$Origem = "C:\TrackerAgentDist",
    [Parameter(Mandatory = $true)][string]$PastaGpo,
    [ValidateRange(1, 10)][int]$Manter = 2
)

$ErrorActionPreference = "Stop"

$exeOrigem = Join-Path $Origem "Instalar-Agente.exe"
$versaoOrigem = Join-Path $Origem "versao.txt"
if (-not (Test-Path $exeOrigem)) { throw "Instalador nao encontrado: $exeOrigem (rode o gerar-pendrive.ps1 antes)." }
if (-not (Test-Path $versaoOrigem)) { throw "versao.txt nao encontrado em $Origem (gerado pelo gerar-pendrive.ps1 a partir de 14/10/2026)." }

$versao = ((Get-Content $versaoOrigem -TotalCount 1) | Out-String).Trim()
# Vai para nome de arquivo e para o .cmd da GPO: nada de espaco, aspas ou caractere de controle.
if ($versao -notmatch '^[0-9A-Za-z.+-]+$') { throw "Versao invalida em ${versaoOrigem}: '$versao'." }

if (-not (Test-Path $PastaGpo)) {
    throw "Pasta da GPO inacessivel: $PastaGpo. Confira o compartilhamento e se a conta deste processo ($env:USERDOMAIN\$env:USERNAME) pode gravar nele."
}

$versaoGpo = Join-Path $PastaGpo "versao.txt"
$publicada = if (Test-Path $versaoGpo) { ((Get-Content $versaoGpo -TotalCount 1) | Out-String).Trim() } else { "" }
if ($publicada -eq $versao) {
    Write-Host "Pasta da GPO ja esta na versao $versao; nada a fazer."
    return
}

$nomeExe = "Instalar-Agente-$versao.exe"
$exeGpo = Join-Path $PastaGpo $nomeExe
$temporario = "$exeGpo.copiando"

Write-Host "Publicando $nomeExe em $PastaGpo (versao anterior: $(if ($publicada) { $publicada } else { 'nenhuma' }))..."
if (Test-Path $temporario) { Remove-Item $temporario -Force }
Copy-Item $exeOrigem $temporario -Force

# Confere o conteudo antes de liberar: copia interrompida nao pode virar a versao oficial.
$hashOrigem = (Get-FileHash $exeOrigem -Algorithm SHA256).Hash
$hashCopia = (Get-FileHash $temporario -Algorithm SHA256).Hash
if ($hashOrigem -ne $hashCopia) {
    Remove-Item $temporario -Force -ErrorAction SilentlyContinue
    throw "A copia na pasta da GPO nao confere com a origem (SHA-256 diferente)."
}
Move-Item $temporario $exeGpo -Force

New-Item -ItemType Directory -Path (Join-Path $PastaGpo "logs") -Force | Out-Null

# POR ULTIMO: a partir daqui as maquinas passam a instalar a versao nova.
$versaoTemp = "$versaoGpo.novo"
Set-Content -Path $versaoTemp -Value $versao -Encoding ASCII -NoNewline
Move-Item $versaoTemp $versaoGpo -Force
Write-Host "versao.txt da GPO agora aponta para $versao."

# Poda os instaladores antigos. Um que esteja em uso numa maquina falha ao apagar: fica para a proxima.
Get-ChildItem $PastaGpo -Filter "Instalar-Agente-*.exe" |
    Where-Object { $_.Name -ne $nomeExe } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip ($Manter - 1) |
    ForEach-Object {
        try { Remove-Item $_.FullName -Force; Write-Host "Removido instalador antigo: $($_.Name)" }
        catch { Write-Warning "Nao foi possivel remover $($_.Name) (em uso?): $($_.Exception.Message)" }
    }
