<#
.SYNOPSIS
  Gera o instalador do Tracker Agent para pendrive: UM unico arquivo Instalar-Agente.exe.

.DESCRIPTION
  Rode na maquina da TI, a partir da raiz do repositorio. Voce so precisa disto quando o
  agente mudar ou quando a configuracao (backend/chave) mudar - no dia a dia basta copiar
  o .exe pronto para o pendrive.

  O script:
    1. publica o agente (win-x64, self-contained - nao exige .NET na maquina alvo);
    2. grava o appsettings.json JA CONFIGURADO junto do agente;
    3. compacta tudo em payload.zip e EMBUTE no executavel do instalador;
    4. apaga o payload.zip (contem a EnrollmentKey) e entrega o .exe no destino.

  O executavel resultante e autocontido: nao precisa de nenhum arquivo ao lado.

.PARAMETER Destino
  Pasta do pendrive (ex.: E:\TrackerAgent) ou uma pasta local para depois copiar.

.PARAMETER BackendUrl
  URL base do backend, ex.: https://tracker.foodsacademy.com.br

.PARAMETER EnrollmentKey
  Chave unica da empresa usada no auto-cadastro. Se omitida, e lida do user-secrets do
  projeto Tracker.Api (chave Enrollment:CompanyKey) sem aparecer na tela.

.PARAMETER Tipo
  Tipo do dispositivo criado no auto-cadastro: Notebook ou Mobile (valores de
  TipoDispositivo que o auto-cadastro aceita; Periferico nao tem agente). Padrao: Notebook.

.PARAMETER Simular
  Se informado, o agente NAO trava a tela de verdade (modo de teste).

.EXAMPLE
  .\deploy\gerar-pendrive.ps1 -Destino E:\TrackerAgent -BackendUrl https://tracker.foodsacademy.com.br
#>
param(
    [Parameter(Mandatory = $true)][string]$Destino,
    [Parameter(Mandatory = $true)][string]$BackendUrl,
    [string]$EnrollmentKey,
    [ValidateSet("Notebook", "Mobile")][string]$Tipo = "Notebook",
    [switch]$Simular
)

$ErrorActionPreference = "Stop"

$raiz = Split-Path -Parent $PSScriptRoot
$projetoAgente = Join-Path $raiz "agents\Tracker.Agent.Windows"
$projetoInstalador = Join-Path $PSScriptRoot "Instalador"
if (-not (Test-Path $projetoAgente)) { throw "Projeto do agente nao encontrado em: $projetoAgente" }

# Sem chave na linha de comando: le do user-secrets local (nao imprime o valor).
if ([string]::IsNullOrWhiteSpace($EnrollmentKey)) {
    Write-Host "Lendo Enrollment:CompanyKey do user-secrets..." -ForegroundColor Cyan
    $linhas = & dotnet user-secrets list --project (Join-Path $raiz "src\Tracker.Api")
    $linha = $linhas | Where-Object { $_ -like "Enrollment:CompanyKey =*" } | Select-Object -First 1
    if (-not $linha) { throw "Enrollment:CompanyKey nao encontrada no user-secrets. Passe -EnrollmentKey." }
    $EnrollmentKey = $linha.Substring($linha.IndexOf("=") + 1).Trim()
    if ([string]::IsNullOrWhiteSpace($EnrollmentKey)) { throw "Enrollment:CompanyKey esta vazia no user-secrets." }
}

$tempPayload = Join-Path $env:TEMP "tracker-payload"
$tempPublish = Join-Path $env:TEMP "tracker-instalador-publish"
$recursos = Join-Path $projetoInstalador "Recursos"
$zip = Join-Path $recursos "payload.zip"

try {
    Write-Host "1/4 Publicando o agente..." -ForegroundColor Cyan
    if (Test-Path $tempPayload) { Remove-Item $tempPayload -Recurse -Force }
    New-Item -ItemType Directory -Path $tempPayload -Force | Out-Null

    & dotnet publish $projetoAgente -c Release -r win-x64 --self-contained true -o $tempPayload | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar o agente (dotnet publish retornou $LASTEXITCODE)." }

    # Nunca leve segredos/estado de runtime de outra maquina.
    foreach ($lixo in @("token.dat", "agent.log", "appsettings.Development.json")) {
        Remove-Item (Join-Path $tempPayload $lixo) -Force -ErrorAction SilentlyContinue
    }

    Write-Host "2/4 Gravando appsettings.json ja configurado..." -ForegroundColor Cyan
    $appsettings = [ordered]@{
        Logging = [ordered]@{
            LogLevel = [ordered]@{
                Default                      = "Information"
                "Microsoft.Hosting.Lifetime" = "Information"
            }
        }
        Agente = [ordered]@{
            BackendBaseUrl           = $BackendUrl
            AgentToken               = ""
            EnrollmentKey            = $EnrollmentKey
            Tipo                     = $Tipo
            HeartbeatIntervalSeconds = 60
            Simular                  = [bool]$Simular
        }
    }
    $appsettings | ConvertTo-Json -Depth 5 | Out-File (Join-Path $tempPayload "appsettings.json") -Encoding utf8

    Write-Host "3/4 Embutindo o agente no executavel do instalador..." -ForegroundColor Cyan
    if (Test-Path $recursos) { Remove-Item $recursos -Recurse -Force }
    New-Item -ItemType Directory -Path $recursos -Force | Out-Null
    Compress-Archive -Path (Join-Path $tempPayload "*") -DestinationPath $zip -CompressionLevel Optimal

    if (Test-Path $tempPublish) { Remove-Item $tempPublish -Recurse -Force }
    & dotnet publish $projetoInstalador -c Release -o $tempPublish | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o instalador (dotnet publish retornou $LASTEXITCODE)." }

    Write-Host "4/4 Entregando o instalador..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $Destino -Force | Out-Null
    Copy-Item (Join-Path $tempPublish "Instalar-Agente.exe") $Destino -Force
    # Acompanham o .exe, mas ele NAO depende de nenhum dos dois:
    # LEIA-ME e instrucao para a TI; Diagnostico-UAC.cmd so serve se a elevacao falhar.
    Copy-Item (Join-Path $PSScriptRoot "pendrive\LEIA-ME.txt") $Destino -Force
    Copy-Item (Join-Path $PSScriptRoot "pendrive\Diagnostico-UAC.cmd") $Destino -Force

    $exe = Join-Path $Destino "Instalar-Agente.exe"
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)

    Write-Host ""
    Write-Host "Pronto." -ForegroundColor Green
    Write-Host "Instalador: $exe ($mb MB, arquivo unico)"
    Write-Host "Backend: $BackendUrl | Tipo: $Tipo | Simular: $([bool]$Simular)"
    Write-Host "Na maquina alvo: duplo clique no .exe (o Windows pede usuario e senha)."
}
finally {
    # O zip contem a EnrollmentKey: nunca deve sobrar no repositorio.
    if (Test-Path $recursos) { Remove-Item $recursos -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $tempPayload) { Remove-Item $tempPayload -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $tempPublish) { Remove-Item $tempPublish -Recurse -Force -ErrorAction SilentlyContinue }
}
