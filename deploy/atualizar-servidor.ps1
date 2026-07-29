<#
.SYNOPSIS
  Atualiza a instalacao de PRODUCAO do Tracker no IIS a partir de um pacote ja publicado.

.DESCRIPTION
  Roda NO SERVIDOR (via self-hosted runner do GitHub Actions), como Administrador.
  Nao compila nada: apenas troca os arquivos do site pelo pacote gerado pelo publicar-servidor.ps1.
  Passos:
    1) valida o pacote de origem
    2) faz backup da instalacao atual (mantem os 5 mais recentes)
    3) para o site + app pool no IIS
    4) copia os arquivos novos (a pasta de dados fica FORA de C:\Tracker e nao e tocada)
    5) inicia o site e faz health check em /health (falha o deploy se nao responder)

  As migracoes de banco sao aplicadas sozinhas quando a aplicacao reinicia (ver Program.cs).

.PARAMETER Origem
  Pasta do pacote publicado (saida do publicar-servidor.ps1 / artifact do CI). Padrao: C:\publish\Tracker

.PARAMETER Destino
  Pasta da instalacao servida pelo IIS. Padrao: C:\Tracker

.PARAMETER Site
  Nome do site no IIS. Padrao: Tracker

.PARAMETER HealthUrl
  URL de health check apos iniciar. Padrao: https://localhost/health

.PARAMETER PastaBackups
  Onde guardar os backups da instalacao anterior. Padrao: C:\TrackerBackups
#>
param(
    [string]$Origem = "C:\publish\Tracker",
    [string]$Destino = "C:\Tracker",
    [string]$Site = "Tracker",
    [string]$HealthUrl = "https://localhost/health",
    [string]$PastaBackups = "C:\TrackerBackups"
)

$ErrorActionPreference = "Stop"
Import-Module WebAdministration

# Valida que a origem e mesmo um pacote publicado da API.
if (-not (Test-Path (Join-Path $Origem "Tracker.Api.dll"))) {
    throw "Pacote invalido em '$Origem' (Tracker.Api.dll nao encontrado). Gere com publicar-servidor.ps1."
}

$pool = (Get-Item "IIS:\Sites\$Site").applicationPool

Write-Host "1/5 - Backup da instalacao atual..."
if (Test-Path $Destino) {
    New-Item -ItemType Directory -Force $PastaBackups | Out-Null
    $carimbo = Get-Date -Format "yyyyMMdd-HHmmss"
    $backup = Join-Path $PastaBackups "Tracker-$carimbo"
    Copy-Item $Destino $backup -Recurse -Force
    Write-Host "   Backup em: $backup"
    # Mantem apenas os 5 backups mais recentes.
    Get-ChildItem $PastaBackups -Directory |
        Sort-Object CreationTime -Descending |
        Select-Object -Skip 5 |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "2/5 - Parando o site '$Site' e o app pool '$pool'..."
if ((Get-Website -Name $Site).State -ne "Stopped") { Stop-Website -Name $Site }
if ((Get-WebAppPoolState -Name $pool).Value -ne "Stopped") { Stop-WebAppPool -Name $pool }
# Espera o w3wp liberar os arquivos.
Start-Sleep -Seconds 4

Write-Host "3/5 - Copiando arquivos novos para '$Destino'..."
New-Item -ItemType Directory -Force $Destino | Out-Null
# A pasta de dados de producao fica em C:\TrackerDados (fora de $Destino), entao limpar aqui e seguro.
Get-ChildItem $Destino -Force | Remove-Item -Recurse -Force
Copy-Item (Join-Path $Origem "*") $Destino -Recurse -Force

Write-Host "4/5 - Iniciando o app pool e o site..."
Start-WebAppPool -Name $pool
Start-Website -Name $Site

Write-Host "5/5 - Health check em '$HealthUrl'..."
# Ignora a validacao de certificado no loopback (compativel com Windows PowerShell 5.1).
Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrackerCertPolicy : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint s, X509Certificate c, WebRequest r, int p) { return true; }
}
"@ -ErrorAction SilentlyContinue
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrackerCertPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$ok = $false
for ($i = 1; $i -le 12; $i++) {
    Start-Sleep -Seconds 3
    try {
        $r = Invoke-WebRequest $HealthUrl -UseBasicParsing -TimeoutSec 10
        if ($r.StatusCode -eq 200 -and $r.Content -match '"status"\s*:\s*"ok"') { $ok = $true; break }
    } catch {
        Write-Host "   tentativa $i/12 ainda sem resposta..."
    }
}

if (-not $ok) {
    throw "Health check FALHOU apos o deploy. Verifique o Visualizador de Eventos (origem 'IIS AspNetCore Module'). Backups em '$PastaBackups'."
}

Write-Host ""
Write-Host "Deploy concluido com sucesso em '$Destino'."
