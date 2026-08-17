<#
.SYNOPSIS
  Gera o APK de release (assinado) do agente Android, criando a keystore na primeira execucao.

.DESCRIPTION
  Roda na maquina de TI. Faz, em um comando so:
    1) localiza o keytool (JDK) e o SDK do Android
    2) cria a keystore de assinatura, se ainda nao existir (a senha e digitada por voce)
    3) escreve o local.properties (fora do Git) com o caminho do SDK e as credenciais
    4) compila o APK de release assinado
    5) mostra versionCode, versionName e o SHA-256 do APK para conferencia no painel

  A SENHA NAO E GRAVADA EM NENHUM LUGAR ALEM DO local.properties, que o .gitignore ja exclui.
  Ela e passada ao keytool por variavel de ambiente temporaria (nunca pela linha de comando,
  que seria visivel na lista de processos).

  ATENCAO A KEYSTORE: guarde o .jks e a senha em cofre, com backup. Perde-los significa nao
  poder mais atualizar o app nos aparelhos ja instalados - o Android recusa uma atualizacao
  assinada com chave diferente, e a saida seria desinstalar e reinstalar aparelho por aparelho.

.PARAMETER Keystore
  Caminho do arquivo .jks. Padrao: Documents\TrackerKeystore\tracker-release.jks
  Fica FORA do repositorio de proposito: worktrees e clones novos nao levam a chave junto.

.PARAMETER Alias
  Alias da chave dentro da keystore. Padrao: tracker

.PARAMETER SdkDir
  Pasta do SDK do Android. Padrao: C:\Android\Sdk

.EXAMPLE
  .\deploy\gerar-apk-agente.ps1
#>
param(
    [string]$Keystore = "$env:USERPROFILE\Documents\TrackerKeystore\tracker-release.jks",
    [string]$Alias = "tracker",
    [string]$SdkDir = "C:\Android\Sdk"
)

$ErrorActionPreference = "Stop"

# Raiz do repositorio a partir da pasta deste script (funciona em worktree ou clone normal).
$raizRepo = Split-Path -Parent $PSScriptRoot
$pastaAgente = Join-Path $raizRepo "agents\Tracker.Agent.Android"
$localProperties = Join-Path $pastaAgente "local.properties"

if (-not (Test-Path $pastaAgente)) { throw "Pasta do agente Android nao encontrada em '$pastaAgente'." }
if (-not (Test-Path $SdkDir)) { throw "SDK do Android nao encontrado em '$SdkDir'. Use -SdkDir para indicar a pasta." }

Write-Host "1/5 - Localizando o keytool (JDK)..."
$keytool = $null
if ($env:JAVA_HOME -and (Test-Path "$env:JAVA_HOME\bin\keytool.exe")) {
    $keytool = "$env:JAVA_HOME\bin\keytool.exe"
} else {
    # Ordem de preferencia: JDK autonomo, depois o JBR que vem com o Android Studio.
    $candidatos = @(
        "C:\Program Files\Eclipse Adoptium\jdk-*\bin\keytool.exe",
        "C:\Program Files\Java\*\bin\keytool.exe",
        "C:\Program Files\Microsoft\jdk*\bin\keytool.exe",
        "C:\Program Files\Android\Android Studio\jbr\bin\keytool.exe"
    )
    foreach ($padrao in $candidatos) {
        $achado = Get-Item $padrao -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($achado) { $keytool = $achado.FullName; break }
    }
}
if (-not $keytool) { throw "keytool nao encontrado. Instale um JDK 17 ou defina JAVA_HOME." }
Write-Host "      $keytool"

# --- Keystore -----------------------------------------------------------------------------
$senhaTexto = $null

if (Test-Path $Keystore) {
    Write-Host "2/5 - Keystore ja existe em '$Keystore'."
    Write-Host "      Use a MESMA senha de sempre: assinatura diferente nao instala sobre o app atual."
    $senhaSegura = Read-Host "      Senha da keystore" -AsSecureString
    $senhaTexto = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($senhaSegura))
} else {
    Write-Host "2/5 - Criando a keystore de assinatura (primeira vez)..."
    Write-Host ""
    Write-Host "      ESTA CHAVE PASSA A VALER PARA TODA A FROTA." -ForegroundColor Yellow
    Write-Host "      Guarde o arquivo e a senha em cofre, com backup. Sem eles, os aparelhos" -ForegroundColor Yellow
    Write-Host "      ja instalados nunca mais recebem atualizacao automatica." -ForegroundColor Yellow
    Write-Host ""

    $senhaSegura = Read-Host "      Defina a senha da keystore (minimo 6 caracteres)" -AsSecureString
    $confirmaSegura = Read-Host "      Repita a senha" -AsSecureString

    $senhaTexto = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($senhaSegura))
    $confirmaTexto = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($confirmaSegura))

    if ($senhaTexto -ne $confirmaTexto) { throw "As senhas nao conferem. Nada foi criado." }
    if ($senhaTexto.Length -lt 6) { throw "O Java exige no minimo 6 caracteres na senha da keystore." }

    New-Item -ItemType Directory -Force (Split-Path -Parent $Keystore) | Out-Null

    # -storepass:env / -keypass:env: a senha vai por variavel de ambiente do processo, e nao
    # pela linha de comando (que apareceria para qualquer um rodando 'tasklist'/Get-Process).
    $env:TRACKER_KS_PASS = $senhaTexto
    try {
        & $keytool -genkeypair -v `
            -keystore $Keystore `
            -alias $Alias `
            -keyalg RSA -keysize 4096 -validity 10000 `
            -storepass:env TRACKER_KS_PASS `
            -keypass:env TRACKER_KS_PASS `
            -dname "CN=Nutrihouse Tracker Agent, OU=TI, O=Nutrihouse, L=Sao Paulo, S=SP, C=BR"
        if ($LASTEXITCODE -ne 0) { throw "keytool falhou (codigo $LASTEXITCODE)." }
    } finally {
        Remove-Item Env:\TRACKER_KS_PASS -ErrorAction SilentlyContinue
    }

    Write-Host "      Keystore criada: $Keystore" -ForegroundColor Green
    Write-Host "      FACA BACKUP DESTE ARQUIVO AGORA." -ForegroundColor Yellow
}

# --- local.properties ---------------------------------------------------------------------
Write-Host "3/5 - Escrevendo o local.properties (fora do Git)..."

# Barras normais: o formato .properties trata a barra invertida como escape.
$sdkNormalizado = $SdkDir -replace '\\', '/'
$keystoreNormalizado = $Keystore -replace '\\', '/'

$conteudo = @"
# Gerado por deploy/gerar-apk-agente.ps1. NAO versionar (ja coberto pelo .gitignore).
sdk.dir=$sdkNormalizado

tracker.keystoreFile=$keystoreNormalizado
tracker.keystorePassword=$senhaTexto
tracker.keyAlias=$Alias
tracker.keyPassword=$senhaTexto
"@

Set-Content -Path $localProperties -Value $conteudo -Encoding UTF8
Write-Host "      $localProperties"

# --- Compilacao -----------------------------------------------------------------------------
Write-Host "4/5 - Compilando o APK de release (pode levar alguns minutos)..."
Push-Location $pastaAgente
try {
    & .\gradlew.bat :app:assembleRelease --console=plain
    if ($LASTEXITCODE -ne 0) { throw "A compilacao falhou (codigo $LASTEXITCODE)." }
} finally {
    Pop-Location
}

# --- Conferencia ------------------------------------------------------------------------------
Write-Host "5/5 - Conferindo o pacote gerado..."
$apk = Join-Path $pastaAgente "app\build\outputs\apk\release\app-release.apk"
if (-not (Test-Path $apk)) { throw "APK nao encontrado em '$apk'." }

# versionCode/versionName sao digitados no painel na hora de publicar: leem-se do build.gradle.kts
# para nao haver divergencia entre o que foi compilado e o que a frota vai comparar.
$gradleTexto = Get-Content (Join-Path $pastaAgente "app\build.gradle.kts") -Raw
$versionCode = [regex]::Match($gradleTexto, 'versionCode\s*=\s*(\d+)').Groups[1].Value
$versionName = [regex]::Match($gradleTexto, 'versionName\s*=\s*"([^"]+)"').Groups[1].Value
$hash = (Get-FileHash $apk -Algorithm SHA256).Hash
$tamanhoMb = [math]::Round((Get-Item $apk).Length / 1MB, 2)

Write-Host ""
Write-Host "APK pronto." -ForegroundColor Green
Write-Host "  Arquivo:     $apk"
Write-Host "  Tamanho:     $tamanhoMb MB"
Write-Host "  versionCode: $versionCode"
Write-Host "  versionName: $versionName"
Write-Host "  SHA-256:     $hash"
Write-Host ""
Write-Host "Proximos passos:"
Write-Host "  1. Instale neste primeiro aparelho pelo cabo (a frota atual nao tem OTA ainda):"
Write-Host "     $SdkDir\platform-tools\adb.exe install -r `"$apk`""
Write-Host "  2. Publique no painel (pagina Agente) informando versionCode $versionCode e versionName $versionName."
Write-Host "     Dai em diante os aparelhos se atualizam sozinhos."
