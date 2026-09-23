<#
.SYNOPSIS
  Prepara uma maquina nova para desenvolver o Tracker (banco em container + segredos de dev).

.DESCRIPTION
  Roda na MAQUINA DE DESENVOLVIMENTO, de qualquer diretorio.
  E idempotente: rodar de novo nao regera nada que ja exista.
  Passos:
    1) confere as ferramentas (dotnet, node e docker)
    2) cria o .env com uma senha aleatoria para o SA (se ainda nao existir)
    3) sobe o SQL Server em container e espera ficar pronto
    4) configura os user-secrets do Tracker.Api (so o que estiver faltando)
    5) instala as dependencias do site
    6) configura o agente Android (local.properties), se o SDK estiver instalado
  As migracoes NAO sao aplicadas aqui: a API as aplica sozinha ao subir (ver Program.cs).

  PRODUCAO NAO USA DOCKER - este script e so para desenvolvimento.

.PARAMETER SemDocker
  Nao sobe container nenhum: assume um SQL Server ja instalado na maquina e mantem a
  connection string do appsettings.json (localhost\SQLEXPRESS). Use quando a maquina ja
  tem o SQL Server Express e voce so quer os segredos de dev configurados.

.PARAMETER SemAndroid
  Nao configura o agente Android. Use quando a maquina nao vai compilar o app (quem mexe so no
  backend e no site nao precisa do Android SDK, que ocupa varios GB).

.PARAMETER IpDev
  IP desta maquina na rede, gravado no local.properties para o APK de debug achar a API local.
  Por padrao e detectado pela interface que tem o gateway padrao; informe a mao se a maquina
  tiver varias redes e a escolhida for a errada.

.PARAMETER Forcar
  Sobrescreve a connection string ja gravada nos user-secrets. Sem isto, uma connection
  string existente e PRESERVADA (para nao sequestrar um ambiente que ja funciona).

.EXAMPLE
  .\deploy\preparar-ambiente-dev.ps1
#>
param(
    [switch]$SemDocker,
    [switch]$SemAndroid,
    [string]$IpDev,
    [switch]$Forcar
)

$ErrorActionPreference = "Stop"

$raiz = Split-Path $PSScriptRoot -Parent
$api = Join-Path $raiz "src\Tracker.Api"
$web = Join-Path $raiz "src\Tracker.Web"
$arquivoEnv = Join-Path $raiz ".env"
$portaBanco = 11433

# ---------------------------------------------------------------------------
# Funcoes auxiliares (declaradas antes do uso: o PowerShell 5.1 exige).
# ---------------------------------------------------------------------------

# Executa um comando externo e devolve o codigo de saida, SEM deixar o stderr dele virar erro
# terminante. Necessario porque o PowerShell 5.1, com $ErrorActionPreference = 'Stop', converte
# cada linha de stderr de um .exe em ErrorRecord quando a saida do script esta redirecionada
# (ex.: '.\preparar-ambiente-dev.ps1 > log.txt 2>&1') - e docker, dotnet e npm escrevem
# PROGRESSO no stderr, nao so erros. Sem isto o script morre no meio de uma execucao normal.
function ExecutarExterno {
    param([scriptblock]$Bloco)
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Bloco | Out-Null } finally { $ErrorActionPreference = $anterior }
    return $LASTEXITCODE
}

# Igual, mas devolve tambem as linhas de saida (para quem precisa ler o resultado).
function ExecutarExternoComSaida {
    param([scriptblock]$Bloco, [switch]$IncluirErro)
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        # -IncluirErro: ferramentas como o 'java -version' escrevem o resultado no STDERR.
        if ($IncluirErro) { $linhas = & $Bloco 2>&1 } else { $linhas = & $Bloco 2>$null }
    } finally { $ErrorActionPreference = $anterior }
    return [pscustomobject]@{ Codigo = $LASTEXITCODE; Linhas = $linhas }
}

# No formato .properties do Java a barra invertida e caractere de escape: gravar
# "C:\Android\Sdk" cru faz o Gradle ler "C:AndroidSdk" (\A e \S nao sao escapes validos) e nao
# achar o SDK. So a barra precisa de escape - ':' dentro do valor e lido literalmente.
function EscaparProperties {
    param([string]$Valor)
    # [char]92 e a barra invertida. Escrita assim de proposito: uma barra literal aqui atravessa
    # varias camadas de escape (editor, shell, .properties) e ja foi corrompida antes.
    $barra = [string][char]92
    return $Valor.Replace($barra, $barra + $barra)
}

# Igual ao ExecutarExterno, mas deixa a saida aparecer na tela (para comandos demorados, em que
# o usuario precisa ver o progresso).
function ExecutarExternoVisivel {
    param([scriptblock]$Bloco)
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    # 'Out-Host' e obrigatorio: sem ele, tudo que o comando escreve no STDOUT entra no valor de
    # retorno da funcao junto com o codigo, e quem fizer '$codigo -ne 0' recebe um array e acusa
    # falha num comando que deu certo. O 'docker compose' escapava disso por escrever no stderr;
    # o 'npm ci' escreve no stdout e quebrava.
    try { & $Bloco | Out-Host } finally { $ErrorActionPreference = $anterior }
    return $LASTEXITCODE
}

# Senha aleatoria que atende a politica do SQL Server (maiuscula, minuscula, digito e
# simbolo). O alfabeto evita ; " ' $ e & de proposito: esses caracteres quebrariam a
# connection string, o arquivo .env ou a linha de comando.
function NovaSenha {
    param([int]$Tamanho = 24)
    $classes = @('ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnopqrstuvwxyz', '23456789', '#%*-_')
    $todos = -join $classes
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 4

    $sortear = {
        param([string]$alfabeto)
        $rng.GetBytes($bytes)
        $valor = [BitConverter]::ToUInt32($bytes, 0)
        $alfabeto[$valor % $alfabeto.Length]
    }

    # Uma de cada classe garante a politica; o resto sai do alfabeto inteiro.
    $caracteres = @()
    foreach ($classe in $classes) { $caracteres += (& $sortear $classe) }
    while ($caracteres.Count -lt $Tamanho) { $caracteres += (& $sortear $todos) }

    # Embaralha para as 4 primeiras posicoes nao serem sempre das mesmas classes.
    return -join ($caracteres | Sort-Object { Get-Random })
}

function NovaChaveAes256 {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    return [Convert]::ToBase64String($bytes)
}

function LerArquivoEnv {
    param([string]$Caminho)
    $mapa = @{}
    if (-not (Test-Path $Caminho)) { return $mapa }
    foreach ($linha in (Get-Content $Caminho)) {
        if ($linha -match '^\s*([^#=]+?)\s*=\s*(.*)$') { $mapa[$Matches[1].Trim()] = $Matches[2].Trim() }
    }
    return $mapa
}

function LerSegredos {
    param([string]$Projeto)
    $mapa = @{}
    $r = ExecutarExternoComSaida { dotnet user-secrets list --project $Projeto }
    if ($r.Codigo -ne 0) { return $mapa }   # projeto ainda sem segredos
    foreach ($linha in $r.Linhas) {
        if ($linha -match '^\s*(.+?)\s*=\s*(.*)$') { $mapa[$Matches[1].Trim()] = $Matches[2].Trim() }
    }
    return $mapa
}

function DefinirSegredo {
    param([string]$Projeto, [hashtable]$Atuais, [string]$Chave, [string]$Valor, [switch]$Sobrescrever)
    if ($Atuais.ContainsKey($Chave) -and -not $Sobrescrever) {
        Write-Host "   . $Chave ja configurado (preservado)"
        return $false
    }
    $codigo = ExecutarExterno { dotnet user-secrets set $Chave $Valor --project $Projeto }
    if ($codigo -ne 0) { throw "Falha ao gravar o user-secret '$Chave'." }
    Write-Host "   + $Chave definido"
    return $true
}

# ---------------------------------------------------------------------------
Write-Host "1/6 - Conferindo as ferramentas..."
# ---------------------------------------------------------------------------
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Falta o .NET 8 SDK. Instale de https://dotnet.microsoft.com/download/dotnet/8.0 e abra um terminal novo."
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw "Falta o Node.js (LTS). Instale de https://nodejs.org e abra um terminal novo."
}
Write-Host ("   dotnet " + (dotnet --version) + " | node " + (node --version))

$comandoCompose = $null
if (-not $SemDocker) {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "Falta o Docker Desktop (https://www.docker.com/products/docker-desktop). Se a maquina ja tem SQL Server instalado, rode com -SemDocker."
    }
    if ((ExecutarExterno { docker info --format '{{.ServerVersion}}' }) -ne 0) {
        # Numa maquina nova a causa quase sempre e o WSL2 ausente: o Docker Desktop instala e
        # abre normalmente, mas o engine nunca fica pronto sem o backend. O sintoma e IGUAL ao
        # de "engine parado", entao checamos para nao mandar a pessoa reabrir o Docker em vao.
        $temWsl = $false
        if (Get-Command wsl -ErrorAction SilentlyContinue) {
            $temWsl = ((ExecutarExterno { wsl --status }) -eq 0)
        }
        if (-not $temWsl) {
            throw ("O Docker Desktop precisa do WSL2, que nao esta instalado nesta maquina. " +
                   "Em um PowerShell COMO ADMINISTRADOR rode 'wsl --install', reinicie o Windows, " +
                   "abra o Docker Desktop e rode este script de novo.")
        }
        throw "O Docker esta instalado mas o engine nao respondeu. Abra o Docker Desktop, espere ficar 'Running' e rode de novo."
    }

    # Compose v2 ('docker compose') e o padrao; v1 ('docker-compose') so em instalacoes antigas.
    if ((ExecutarExterno { docker compose version }) -eq 0) { $comandoCompose = @('docker', 'compose') }
    elseif (Get-Command docker-compose -ErrorAction SilentlyContinue) { $comandoCompose = @('docker-compose') }
    else { throw "Docker sem o plugin Compose. Atualize o Docker Desktop." }
    Write-Host ("   docker OK (" + ($comandoCompose -join ' ') + ")")
}

# ---------------------------------------------------------------------------
Write-Host "2/6 - Arquivo .env (senha do SA do banco de dev)..."
# ---------------------------------------------------------------------------
$valoresEnv = LerArquivoEnv $arquivoEnv
if (-not $valoresEnv.ContainsKey('MSSQL_SA_PASSWORD')) {
    $senhaSa = NovaSenha
    $conteudo = @(
        "# Gerado por deploy/preparar-ambiente-dev.ps1. NAO versionar (esta no .gitignore).",
        "# Senha do SA do SQL Server de DESENVOLVIMENTO que roda em container.",
        "MSSQL_SA_PASSWORD=$senhaSa"
    )
    Set-Content $arquivoEnv $conteudo -Encoding ascii
    Write-Host "   .env criado com uma senha aleatoria."
} else {
    $senhaSa = $valoresEnv['MSSQL_SA_PASSWORD']
    Write-Host "   .env ja existe (senha preservada)."
}

# ---------------------------------------------------------------------------
Write-Host "3/6 - Banco de dados..."
# ---------------------------------------------------------------------------
if ($SemDocker) {
    Write-Host "   -SemDocker: pulando o container (usando o SQL Server ja instalado na maquina)."
} else {
    Push-Location $raiz
    try {
        # --quiet-pull: sem ele o download da imagem (~1,5 GB) despeja milhares de linhas de
        # progresso, escondendo as mensagens do proprio script.
        $codigo = ExecutarExternoVisivel {
            & $comandoCompose[0] $comandoCompose[1..($comandoCompose.Count - 1)] up -d --quiet-pull
        }
        if ($codigo -ne 0) { throw "Falha ao subir o container do banco (codigo $codigo)." }
    } finally { Pop-Location }

    # O SQL Server aceita conexao TCP alguns segundos ANTES de estar pronto para consultas:
    # esperamos o healthcheck do compose, nao so a porta abrir.
    Write-Host "   Esperando o banco ficar pronto (ate 3 min na primeira vez)..."
    $pronto = $false
    for ($i = 1; $i -le 36; $i++) {
        $estado = (ExecutarExternoComSaida { docker inspect --format '{{.State.Health.Status}}' tracker-db }).Linhas
        if ($estado -eq 'healthy') { $pronto = $true; break }
        if ($estado -eq 'unhealthy') { throw "O container tracker-db subiu com problema. Veja: docker logs tracker-db" }
        Start-Sleep -Seconds 5
    }
    if (-not $pronto) { throw "O banco nao ficou pronto a tempo. Veja: docker logs tracker-db" }
    Write-Host "   Banco pronto em localhost,$portaBanco."
}

# ---------------------------------------------------------------------------
Write-Host "4/6 - Segredos de desenvolvimento (user-secrets do Tracker.Api)..."
# ---------------------------------------------------------------------------
& dotnet user-secrets init --project $api | Out-Null
$segredos = LerSegredos $api

# ATENCAO: a chave de criptografia so e gerada UMA vez. Regenera-la tornaria ilegiveis os
# CPFs ja gravados no banco (AES-GCM, ver EncryptedStringConverter) - por isso nunca
# sobrescrevemos uma chave existente.
DefinirSegredo $api $segredos 'Encryption:CpfKeyBase64' (NovaChaveAes256) | Out-Null
DefinirSegredo $api $segredos 'Enrollment:CompanyKey'   (NovaSenha -Tamanho 32) | Out-Null
DefinirSegredo $api $segredos 'Seed:AdmEmail'           'adm@local.dev' | Out-Null
DefinirSegredo $api $segredos 'Seed:AdmNome'            'ADM Local' | Out-Null

$senhaAdm = NovaSenha -Tamanho 16
$senhaAdmNova = DefinirSegredo $api $segredos 'Seed:AdmSenha' $senhaAdm

if (-not $SemDocker) {
    $connectionString = "Server=localhost,$portaBanco;Database=TrackerDb;User Id=sa;Password=$senhaSa;TrustServerCertificate=True;Encrypt=False"
    if ($segredos.ContainsKey('ConnectionStrings:TrackerDb') -and -not $Forcar) {
        Write-Host "   ! ConnectionStrings:TrackerDb ja existe e foi PRESERVADA."
        Write-Host "     Para apontar para o container, rode de novo com -Forcar."
    } else {
        DefinirSegredo $api $segredos 'ConnectionStrings:TrackerDb' $connectionString -Sobrescrever:$Forcar | Out-Null
    }
}

# ---------------------------------------------------------------------------
Write-Host "5/6 - Dependencias do site..."
# ---------------------------------------------------------------------------
if (Test-Path (Join-Path $web "node_modules")) {
    Write-Host "   node_modules ja existe (pulando)."
} else {
    Push-Location $web
    try {
        $codigo = ExecutarExternoVisivel { npm ci }
        if ($codigo -ne 0) { throw "Falha no npm ci (codigo $codigo)." }
    } finally { Pop-Location }
}

# ---------------------------------------------------------------------------
Write-Host "6/6 - Agente Android (opcional)..."
# ---------------------------------------------------------------------------
$androidPronto = $false
if ($SemAndroid) {
    Write-Host "   -SemAndroid: pulando."
} else {
    $sdk = @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, 'C:\Android\Sdk', (Join-Path $env:LOCALAPPDATA 'Android\Sdk')) |
        Where-Object { $_ -and (Test-Path (Join-Path $_ 'platforms')) } |
        Select-Object -First 1

    if (-not $sdk) {
        # Nao e erro: quem mexe so no backend/site nao precisa do SDK (varios GB).
        Write-Host "   Android SDK nao encontrado - o agente Android fica de fora."
        Write-Host "   Para habilitar: instale o JDK 17 e o Android SDK (API 34 + build-tools 34.0.0),"
        Write-Host "   depois rode este script de novo. Detalhes em docs/agentes/agente-android.md."
    } else {
        Write-Host "   SDK: $sdk"

        # O Android Gradle Plugin exige JDK 17+. 'java -version' escreve no STDERR.
        $java = if ($env:JAVA_HOME -and (Test-Path (Join-Path $env:JAVA_HOME 'bin\java.exe'))) {
            Join-Path $env:JAVA_HOME 'bin\java.exe'
        } elseif (Get-Command java -ErrorAction SilentlyContinue) {
            (Get-Command java).Source
        } else { $null }

        $versaoJava = 0
        if ($java) {
            $saidaJava = (ExecutarExternoComSaida { & $java -version } -IncluirErro).Linhas | Out-String
            if ($saidaJava -match 'version "(\d+)') { $versaoJava = [int]$Matches[1] }
        }
        if ($versaoJava -lt 17) {
            Write-Host "   ! JDK 17 nao encontrado (achado: $(if ($versaoJava) { $versaoJava } else { 'nenhum' }))."
            Write-Host "     O build do app vai falhar ate instalar o JDK 17 e definir JAVA_HOME."
        } else {
            Write-Host "   JDK $versaoJava OK"
        }

        # local.properties fica FORA do Git (tem caminhos e, em quem publica, senhas de keystore).
        # Escrevemos apenas as chaves AUSENTES: um local.properties ja configurado - com keystore,
        # por exemplo - nunca e sobrescrito.
        $arquivoLocalProps = Join-Path $raiz 'agents\Tracker.Agent.Android\local.properties'
        $props = LerArquivoEnv $arquivoLocalProps
        $novas = @()

        if (-not $props.ContainsKey('sdk.dir')) {
            $novas += "sdk.dir=" + (EscaparProperties $sdk)
        }

        if (-not $props.ContainsKey('tracker.backendBaseUrl')) {
            # IMPORTANTE: sem esta chave o build DEBUG cai para a URL de producao (ver
            # app/build.gradle.kts). Um APK de teste instalado num aparelho se auto-cadastraria
            # no inventario REAL. Por isso apontamos para a API desta maquina.
            $ip = $IpDev
            if (-not $ip) {
                $ip = (Get-NetIPConfiguration |
                       Where-Object { $_.IPv4DefaultGateway -and $_.IPv4Address } |
                       Select-Object -First 1).IPv4Address.IPAddress
            }
            if ($ip) {
                $novas += "tracker.backendBaseUrl=http://${ip}:5171"
                Write-Host "   IP desta maquina para o APK de debug: $ip (troque com -IpDev se estiver errado)"
            } else {
                Write-Host "   ! Nao consegui detectar o IP da maquina. Defina tracker.backendBaseUrl a mao"
                Write-Host "     em $arquivoLocalProps - sem ele o APK de DEBUG aponta para PRODUCAO."
            }
        }

        if ($novas.Count -gt 0) {
            if (-not (Test-Path $arquivoLocalProps)) {
                $cabecalho = @(
                    "# Gerado por deploy/preparar-ambiente-dev.ps1. NAO versionar (esta no .gitignore).",
                    "# Configuracao desta maquina para compilar o agente Android.",
                    "# O release exige, alem disto, as chaves de keystore - ver docs/agentes/agente-android.md."
                )
                Set-Content $arquivoLocalProps ($cabecalho + $novas) -Encoding ascii
            } else {
                Add-Content $arquivoLocalProps $novas -Encoding ascii
            }
            foreach ($linha in $novas) { Write-Host ("   + " + ($linha -split '=')[0] + " definido") }
        } else {
            Write-Host "   local.properties ja configurado (preservado)."
        }
        $androidPronto = $true
    }
}

# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Ambiente pronto."
Write-Host ""
if ($senhaAdmNova) {
    Write-Host "  LOGIN ADM INICIAL (criado no primeiro start da API):"
    Write-Host "    e-mail: adm@local.dev"
    Write-Host "    senha : $senhaAdm"
    Write-Host "  Anote agora: esta senha nao volta a ser exibida. Se perder, redefina com:"
    Write-Host "    dotnet run --project src/Tracker.Api -- --redefinir-senha-adm --senha ""NOVA_SENHA"""
    Write-Host ""
}
Write-Host "  Suba a API : dotnet run --project src/Tracker.Api --urls http://localhost:5171"
Write-Host "  Suba o site: npm --prefix src/Tracker.Web run dev     (http://localhost:5173)"
if ($androidPronto) {
    Write-Host ""
    Write-Host "  Agente Android: cd agents/Tracker.Agent.Android; ./gradlew :app:assembleDebug"
    Write-Host "  Para o APARELHO alcancar a API, ela precisa escutar fora do localhost:"
    Write-Host "    dotnet run --project src/Tracker.Api --urls http://0.0.0.0:5171   (+ liberar a porta no firewall)"
    Write-Host "  O APK de RELEASE exige a keystore da frota, que NAO esta no repositorio e nao e"
    Write-Host "  gerada aqui de proposito: assinatura diferente duplica os aparelhos no inventario."
}
Write-Host ""
if (-not $SemDocker) {
    Write-Host "  Parar o banco   : docker compose stop"
    Write-Host "  Apagar os dados : docker compose down -v"
    Write-Host "  Voltar ao SQL Express local:"
    Write-Host "    dotnet user-secrets remove ConnectionStrings:TrackerDb --project src/Tracker.Api"
}
