<#
.SYNOPSIS
  Configura no GitHub os secrets que o workflow "APK Agente Android" usa para assinar o release.

.DESCRIPTION
  Roda UMA VEZ, na maquina que tem a keystore da frota. Le as credenciais de um local.properties
  ja existente e envia quatro secrets para o repositorio:
    ANDROID_KEYSTORE_BASE64    - o arquivo .jks inteiro
    ANDROID_KEYSTORE_PASSWORD  - senha do keystore
    ANDROID_KEY_ALIAS          - alias da chave
    ANDROID_KEY_PASSWORD       - senha da chave

  Nenhum valor e exibido na tela, gravado em disco ou passado na linha de comando: cada secret vai
  pela ENTRADA PADRAO do 'gh'. Argumento de linha de comando apareceria no tasklist para qualquer
  usuario logado na maquina.

  Depois disto, quem precisar de um APK de release nao precisa mais ter a keystore: roda o
  workflow "APK Agente Android" pela aba Actions. A keystore continua sendo insubstituivel e
  precisa de backup proprio - o secret e uma copia de trabalho, NAO um backup.

.PARAMETER LocalProperties
  Caminho do local.properties com tracker.keystoreFile, tracker.keystorePassword,
  tracker.keyAlias e tracker.keyPassword.

.PARAMETER Simular
  Le e valida tudo (inclusive abrindo a keystore com a senha) mas NAO envia nada ao GitHub.

.EXAMPLE
  .\deploy\configurar-secrets-android.ps1 -LocalProperties C:\caminho\local.properties -Simular
#>
param(
    [Parameter(Mandatory = $true)][string]$LocalProperties,
    [string]$Repo,
    [switch]$Simular
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Leitura no formato .properties do Java: a barra invertida e caractere de escape, entao
# "C\:\\Nutrihouse" no arquivo significa "C:\Nutrihouse". Ler sem desescapar devolve uma senha
# diferente da real - e o erro aparece so la na frente, como "keystore password was incorrect".
# ---------------------------------------------------------------------------
function DesescaparProperties {
    param([string]$Valor)
    $barra = [char]92
    $sb = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $Valor.Length; $i++) {
        $c = $Valor[$i]
        if ($c -ne $barra) { [void]$sb.Append($c); continue }
        $i++
        if ($i -ge $Valor.Length) { break }
        $seguinte = $Valor[$i]
        switch ($seguinte) {
            'n' { [void]$sb.Append([char]10) }
            't' { [void]$sb.Append([char]9) }
            'r' { [void]$sb.Append([char]13) }
            'f' { [void]$sb.Append([char]12) }
            'u' {
                if ($i + 4 -lt $Valor.Length) {
                    [void]$sb.Append([char][Convert]::ToInt32($Valor.Substring($i + 1, 4), 16))
                    $i += 4
                }
            }
            default { [void]$sb.Append($seguinte) }
        }
    }
    return $sb.ToString()
}

function LerProperties {
    param([string]$Caminho)
    $mapa = @{}
    $barra = [char]92
    foreach ($linha in (Get-Content $Caminho)) {
        $texto = $linha.TrimStart()
        if ($texto -eq '' -or $texto.StartsWith('#') -or $texto.StartsWith('!')) { continue }

        # O separador e o primeiro '=' ou ':' que NAO esteja escapado.
        $corte = -1
        for ($i = 0; $i -lt $texto.Length; $i++) {
            if ($texto[$i] -eq $barra) { $i++; continue }
            if ($texto[$i] -eq '=' -or $texto[$i] -eq ':') { $corte = $i; break }
        }
        if ($corte -lt 0) { continue }

        $chave = (DesescaparProperties $texto.Substring(0, $corte)).Trim()
        $valor = DesescaparProperties $texto.Substring($corte + 1).TrimStart()
        $mapa[$chave] = $valor
    }
    return $mapa
}

# Envia um secret pela ENTRADA PADRAO do gh, sem newline no fim (um '\n' extra viraria parte da
# senha e o build no CI falharia com "keystore password was incorrect", sem pista da causa).
function EnviarSecret {
    param([string]$Nome, [string]$Valor, [string]$Repositorio)
    $argumentos = "secret set $Nome"
    if ($Repositorio) { $argumentos += " --repo $Repositorio" }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'gh'
    $psi.Arguments = $argumentos
    $psi.RedirectStandardInput = $true
    $psi.UseShellExecute = $false
    $processo = [System.Diagnostics.Process]::Start($psi)
    $processo.StandardInput.Write($Valor)
    $processo.StandardInput.Close()
    $processo.WaitForExit()
    if ($processo.ExitCode -ne 0) { throw "Falha ao gravar o secret $Nome (codigo $($processo.ExitCode))." }
    Write-Host "   + $Nome"
}

# ---------------------------------------------------------------------------
Write-Host "1/4 - Lendo as credenciais..."
# ---------------------------------------------------------------------------
if (-not (Test-Path $LocalProperties)) { throw "Arquivo nao encontrado: $LocalProperties" }
$props = LerProperties $LocalProperties

foreach ($chave in 'tracker.keystoreFile', 'tracker.keystorePassword', 'tracker.keyAlias', 'tracker.keyPassword') {
    if ([string]::IsNullOrWhiteSpace($props[$chave])) { throw "Falta '$chave' em $LocalProperties." }
}
$keystore = $props['tracker.keystoreFile']
if (-not (Test-Path $keystore)) { throw "Keystore nao encontrada: $keystore" }
Write-Host "   keystore: $keystore"
Write-Host ("   alias   : " + $props['tracker.keyAlias'])

# ---------------------------------------------------------------------------
Write-Host "2/4 - Conferindo que as senhas abrem a keystore..."
# ---------------------------------------------------------------------------
# Falhar aqui e MUITO melhor que descobrir no CI: la o erro seria um build quebrado sem pista.
$ks = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2Collection
try {
    $store = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        [System.IO.File]::ReadAllBytes($keystore), $props['tracker.keystorePassword'])
    $impressao = $store.GetCertHashString('SHA256').ToLower()
} catch {
    # JKS nem sempre e lido pelo .NET; nesse caso confirmamos pelo keytool do JDK.
    $keytool = if ($env:JAVA_HOME) { Join-Path $env:JAVA_HOME 'bin\keytool.exe' } else { 'keytool' }
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $saida = $props['tracker.keystorePassword'] | & $keytool -list -keystore $keystore 2>&1 | Out-String
    } finally { $ErrorActionPreference = $anterior }
    if ($saida -match 'password was incorrect|senha.*incorreta') {
        throw "A senha do arquivo NAO abre a keystore. Confira $LocalProperties."
    }
    $impressao = $null
}
Write-Host "   senhas conferidas."
if ($impressao) { Write-Host "   SHA-256 do certificado: $impressao" }

# ---------------------------------------------------------------------------
Write-Host "3/4 - Conferindo o gh..."
# ---------------------------------------------------------------------------
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "O GitHub CLI (gh) nao esta instalado. Baixe em https://cli.github.com e rode 'gh auth login'."
}
# NAO use 'gh auth status' para isto: ele devolve codigo 1 se QUALQUER conta configurada estiver
# com token invalido, mesmo que a conta ATIVA esteja perfeita (acontece quando sobrou um login
# antigo na maquina). 'gh api user' responde pela conta ativa, que e a que vai gravar o secret.
$anterior = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try { $conta = (& gh api user --jq .login 2>$null | Out-String).Trim() } finally { $ErrorActionPreference = $anterior }
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($conta)) {
    throw "O gh nao esta autenticado (ou sem rede). Rode 'gh auth login'."
}
Write-Host "   gh autenticado como $conta."

# ---------------------------------------------------------------------------
Write-Host "4/4 - Enviando os secrets..."
# ---------------------------------------------------------------------------
if ($Simular) {
    Write-Host "   -Simular: nada foi enviado. Estes seriam gravados:"
    Write-Host "     ANDROID_KEYSTORE_BASE64   (arquivo de $((Get-Item $keystore).Length) bytes)"
    Write-Host "     ANDROID_KEYSTORE_PASSWORD (oculta)"
    Write-Host ("     ANDROID_KEY_ALIAS         = " + $props['tracker.keyAlias'])
    Write-Host "     ANDROID_KEY_PASSWORD      (oculta)"
} else {
    EnviarSecret 'ANDROID_KEYSTORE_BASE64'   ([Convert]::ToBase64String([System.IO.File]::ReadAllBytes($keystore))) $Repo
    EnviarSecret 'ANDROID_KEYSTORE_PASSWORD' $props['tracker.keystorePassword'] $Repo
    EnviarSecret 'ANDROID_KEY_ALIAS'         $props['tracker.keyAlias'] $Repo
    EnviarSecret 'ANDROID_KEY_PASSWORD'      $props['tracker.keyPassword'] $Repo
}

Write-Host ""
Write-Host "Pronto. Gere o APK pela aba Actions -> workflow 'APK Agente Android' -> Run workflow."
Write-Host ""
Write-Host "LEMBRE: o secret e uma COPIA DE TRABALHO, nao um backup. A keystore em"
Write-Host "  $keystore"
Write-Host "continua insubstituivel - guarde uma copia dela, e das senhas, fora desta maquina."
