<#
.SYNOPSIS
  Gera o pacote de publicacao da plataforma (site + API juntos) pronto para hospedar no servidor.

.DESCRIPTION
  Rode na MAQUINA DE BUILD (que tem .NET 8 SDK e Node.js). O resultado e uma pasta autocontida
  que voce copia para o servidor e publica no IIS.
  Passos automatizados: build do React -> copia para wwwroot da API -> dotnet publish.

.PARAMETER Saida
  Pasta de saida do pacote publicado. Padrao: C:\publish\Tracker
#>
param(
    [string]$Saida = "C:\publish\Tracker"
)

$ErrorActionPreference = "Stop"

$raiz = Split-Path $PSScriptRoot -Parent
$web = Join-Path $raiz "src\Tracker.Web"
$api = Join-Path $raiz "src\Tracker.Api"
$wwwroot = Join-Path $api "wwwroot"

Write-Host "1/3 - Compilando o site (React)..."
Push-Location $web
try {
    if (-not (Test-Path (Join-Path $web "node_modules"))) { npm install }
    npm run build
} finally { Pop-Location }

Write-Host "2/3 - Copiando o site para wwwroot da API..."
New-Item -ItemType Directory -Force $wwwroot | Out-Null
Get-ChildItem $wwwroot -Force | Remove-Item -Recurse -Force
Copy-Item (Join-Path $web "dist\*") $wwwroot -Recurse -Force

Write-Host "3/3 - Publicando a API (inclui o site)..."
dotnet publish $api -c Release -o $Saida

Write-Host ""
Write-Host "Pronto! Pacote gerado em: $Saida"
Write-Host "Copie essa pasta para o servidor e siga o guia docs/deploy-servidor-onpremises.md."
