<#
.SYNOPSIS
  Gera o pacote de publicacao da plataforma (site + API juntos) pronto para hospedar no servidor.

.DESCRIPTION
  Rode na MAQUINA DE BUILD (que tem .NET 8 SDK e Node.js). O resultado e uma pasta autocontida
  que voce copia para o servidor e publica no IIS.
  Passos automatizados: build do React -> copia para wwwroot da API -> dotnet publish.

.PARAMETER Saida
  Pasta de saida do pacote publicado. Padrao: C:\publish\Tracker

.PARAMETER SemBuild
  Reaproveita a compilacao Release ja existente em vez de compilar de novo no publish.
  Use SOMENTE quando um passo anterior ja rodou 'dotnet build -c Release' nesta mesma copia
  do codigo (e o caso do CI). Em uma maquina de dev, sem build previo, o publish falha.
#>
param(
    [string]$Saida = "C:\publish\Tracker",
    [switch]$SemBuild
)

$ErrorActionPreference = "Stop"

$raiz = Split-Path $PSScriptRoot -Parent
$web = Join-Path $raiz "src\Tracker.Web"
$api = Join-Path $raiz "src\Tracker.Api"
$wwwroot = Join-Path $api "wwwroot"

Write-Host "1/3 - Compilando o site (React)..."
Push-Location $web
try {
    # No CI a pasta node_modules vem do cache local do servidor (ver deploy-producao.yml),
    # entao este 'npm install' so roda em maquina de dev sem dependencias instaladas.
    if (-not (Test-Path (Join-Path $web "node_modules"))) {
        npm install
        if ($LASTEXITCODE -ne 0) { throw "Falha no npm install (codigo $LASTEXITCODE)." }
    }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Falha no build do site (codigo $LASTEXITCODE)." }
} finally { Pop-Location }

Write-Host "2/3 - Copiando o site para wwwroot da API..."
New-Item -ItemType Directory -Force $wwwroot | Out-Null
Get-ChildItem $wwwroot -Force | Remove-Item -Recurse -Force
Copy-Item (Join-Path $web "dist\*") $wwwroot -Recurse -Force

Write-Host "3/3 - Publicando a API (inclui o site)..."
# O publish le o wwwroot do disco AGORA (passo 2), entao o site novo entra no pacote mesmo
# com -SemBuild; arquivos do site antigo que sumiram sao removidos da pasta de saida.
$argumentos = @($api, "-c", "Release", "-o", $Saida)
if ($SemBuild) { $argumentos += @("--no-build", "--no-restore") }
dotnet publish @argumentos
# $ErrorActionPreference nao captura falha de executavel nativo: sem esta checagem, um publish
# quebrado passaria batido e o deploy publicaria o pacote anterior.
if ($LASTEXITCODE -ne 0) { throw "Falha no dotnet publish (codigo $LASTEXITCODE)." }

Write-Host ""
Write-Host "Pronto! Pacote gerado em: $Saida"
Write-Host "Copie essa pasta para o servidor e siga o guia docs/deploy-servidor-onpremises.md."
