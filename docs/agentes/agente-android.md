# Agente Android (tablets e celulares)

Agente do Tracker para aparelhos Android. Faz o mesmo que o agente Windows — auto-cadastro,
heartbeat com localização, comandos remotos por SignalR e bloqueio persistente — adaptado às
regras do Android.

Projeto: `agents/Tracker.Agent.Android/` (Kotlin, minSdk 24, targetSdk 34).

---

## O que o Android permite de verdade

Esta é a parte que precisa ficar clara antes de qualquer decisão de frota. **Nenhum app Android
comum é impossível de remover.** Existem dois níveis, e eles mudam completamente o que a TI pode
garantir:

| | **Device Admin** (APK instalado) | **Device Owner** (QR em aparelho zerado) |
|---|---|---|
| Instalação | APK num aparelho já em uso | Exige restauração de fábrica |
| Bloqueio remoto (`lockNow`) | ✅ Funciona | ✅ Funciona |
| Desinstalação pela Play Store/Configurações | 🚫 Bloqueada pelo Android enquanto o admin estiver ativo | 🚫 Bloqueada de fato (`setUninstallBlocked`) |
| Usuário desativar o admin e então desinstalar | ⚠️ **Possível** — o agente detecta e alerta a TI | 🚫 Impossível |
| Restauração de fábrica | ⚠️ Possível | 🚫 Bloqueada |
| Modo quiosque na tela de bloqueio | 🚫 Indisponível | ✅ Disponível |
| Número de série real e IMEI | 🚫 Indisponíveis (usa `ANDROID_ID`) | ✅ Disponíveis |

O mesmo APK atende aos dois modos: ele detecta em qual está e reporta o nível ao painel
(`NivelProtecao`), que mostra à TI, por aparelho, se a proteção é forte ou fraca.

### Como o agente compensa a fraqueza do Device Admin

- `onDisableRequested` mostra um aviso quando o usuário abre a tela de desativação e **reporta a
  tentativa ao backend na hora**.
- `onDisabled` reporta a desativação efetiva; o backend marca o aparelho como `NivelProtecao =
  Nenhuma` e cria um alerta na aba "Agente & Bloqueio".
- O heartbeat reporta o nível a cada ciclo: se ele cair, o backend gera um alerta
  `ProtecaoRebaixada` mesmo que o alerta imediato não tenha chegado (aparelho sem rede).

Ou seja: não impede, mas a TI **sabe em minutos**.

---

## Como o bloqueio funciona

O objetivo é deixar o aparelho **indisponível**, não apenas travar a tela. Travar a tela não
resolve: o usuário digita a senha dele e volta a usar o aparelho normalmente.

1. **Janela de sobreposição** (`OverlayBloqueio`, `TYPE_APPLICATION_OVERLAY`) — o mecanismo
   principal. Fica acima de **qualquer** aplicativo, inclusive da tela inicial, e consome todos os
   toques. Reaplicada a cada 2 segundos pelo `AgenteService`, a mesma cadência do agente Windows.
   Mostra o patrimônio e a **série digitada no registro** (desde a 1.1.3; a mesma aparece na
   tela de diagnóstico), para a TI identificar o aparelho sem liberá-lo.
2. **`lockNow()` uma única vez** ao aplicar o bloqueio, só para apagar a tela na hora e tirar o
   aparelho da mão do usuário. Não é repetido: em ciclo, daria a impressão de "só travou a tela".
3. **Modo quiosque** quando há Device Owner (desde a 1.1.3): a `BloqueioActivity` sobe em
   **lock task** (`startLockTask`) **por baixo** da sobreposição, o que fecha também Home,
   Recentes, a barra de status e as configurações rápidas. Ao desbloquear, o agente chama
   `stopLockTask`.

O bloqueio é **persistente**, como no Windows: fica gravado localmente (`Armazenamento.bloqueado`)
e é reaplicado após reiniciar (`BootReceiver`), pelo `VigilanteWorker` e ao abrir o app, mesmo
offline. **A reaplicação depende só desse sinal, nunca do token**: o 401 apaga o token, mas não
mexe no bloqueio. Só a TI o remove, pelo botão Desbloquear (ver
[Bloqueado e sem token](#bloqueado-e-sem-token-aparelho-preso) para o caso em que o Desbloquear
não consegue mais chegar).

Se o app for reinstalado ou os dados limpos, o sinal local se perde — nesse caso o campo
`deveEstarBloqueado` do heartbeat traz o estado do servidor e o agente **reaplica a trava** e
registra o alerta `BloqueioReaplicado`. O servidor calcula esse campo pelo **último comando**
Bloquear/Desbloquear emitido (não só pelo status), e o agente **ignora** a resposta de um
heartbeat que saiu antes de um comando ser aplicado (contador `geracaoComandos`): sem isso, o
heartbeat em voo desfazia o Bloquear que tinha acabado de chegar pelo hub. Comandos repetidos
(hub + heartbeat) são deduplicados pelo `id` (últimos 50). Detalhes em
[protocolo-agente.md](protocolo-agente.md).

### Onde o agente guarda o estado

| Arquivo (SharedPreferences) | Conteúdo | Criptografia |
|---|---|---|
| `tracker-agente` | token, id do dispositivo, série digitada, patrimônio | `EncryptedSharedPreferences` (Keystore) |
| `tracker-agente-fallback` | os mesmos segredos, **só** se o Keystore falhar | nenhuma |
| `tracker-agente-estado` | sinal `bloqueado` e o marcador de fallback (`usarArquivoSimples`) | nenhuma, de propósito |

- A escolha "criptografado ou simples" é feita **uma vez por processo**, com novas tentativas
  curtas (falha transitória do Keystore logo após o boot é comum). Se ainda falhar, o marcador
  `usarArquivoSimples` é gravado e **todas** as execuções seguintes usam o arquivo simples, sem
  alternar de volta. Chaves que versões antigas deixaram no arquivo simples são migradas.
- O sinal `bloqueado` fica **fora** do `EncryptedSharedPreferences`: amarrado ao Keystore, um
  aparelho bloqueado voltaria livre no dia em que o Keystore corrompesse (acontece após
  atualização do sistema em alguns aparelhos). É gravado com `commit()` síncrono. Na primeira
  execução da 1.1.3, o valor antigo é migrado — bloqueado em qualquer arquivo = bloqueado.

### Serviço em primeiro plano (Android 14)

O `AgenteService` roda em primeiro plano e declara no manifest
`foregroundServiceType="location|specialUse"`, com a permissão
`FOREGROUND_SERVICE_SPECIAL_USE` e a propriedade `PROPERTY_SPECIAL_USE_FGS_SUBTYPE`. O tipo é
**escolhido em tempo de execução**: `location` só quando a permissão de localização está
concedida; senão `specialUse`. No Android 14, declarar `location` sem permissão — ou subir do
`BOOT_COMPLETED` sem direito de uso em primeiro plano — lança `SecurityException`; antes da
1.1.3 isso virava loop de crash e o bloqueio parava de ser reaplicado. Agora o agente tenta as
alternativas, chama `startForeground` em todo `onStartCommand` e nunca deixa a exceção derrubar
o processo.

### Wi-Fi na tela de bloqueio

Um aparelho bloqueado e sem rede **nunca receberia o comando de Desbloquear** — ficaria travado
para sempre. Por isso a tela tem um botão de Wi-Fi, igual à do agente Windows. O comportamento
muda conforme o nível de proteção, porque o Android 10 tirou dos apps comuns a capacidade de
conectar a uma rede informando SSID e senha:

| | **Device Owner** | **Device Admin** |
|---|---|---|
| Como conecta | Lista de redes, senha e botão Conectar **dentro** da tela de bloqueio | Botão abre o **painel de Wi-Fi do sistema** |
| Sai da tela de bloqueio? | Não | Só enquanto o painel está aberto (teto de **60 segundos**) |

No modo Device Owner, a lista espera o resultado de uma varredura nova
(`SCAN_RESULTS_AVAILABLE_ACTION`, até 8 s, fora da thread principal) em vez de ler o cache logo
após o `startScan` — o cache de um aparelho ocioso costuma vir vazio ou só com a rede atual.

No modo Device Admin, o painel usado é o `Settings.Panel.ACTION_WIFI` (Android 10+), que mostra
**apenas** a lista de redes — não dá acesso ao resto das Configurações, então o usuário não
consegue usá-lo para desativar o agente. Enquanto o painel está aberto a sobreposição sai da frente
(senão o painel ficaria inacessível). O painel é aberto pela `PainelWifiActivity`, uma tela
invisível que fica **pausada por baixo** dele (o painel é translúcido): quando o usuário fecha o
painel ela volta a `onResume`, e quando ele tenta sair (Home, Recentes, "Mais configurações",
notificação, tela apagada) ela vai a `onStop` — nos dois casos a trava volta **na hora**. Os 60
segundos ficam só como teto. Antes a sobreposição saía por 60 s fixos e bastava apertar Home para
usar o aparelho nesse tempo (revisão de 24/09/2026). No Android 9 ou anterior não há painel
restrito — abre a tela cheia de Configurações de Wi-Fi e vale o prazo inteiro. O bloqueio em si
nunca é removido: o sinal local continua ligado o tempo todo.

> **Requisito absoluto:** a permissão "Sobrepor outros apps". Sem ela o Android recusa a janela e
> o aparelho **não** fica indisponível — o agente registra erro no log e o painel mostra a falha.
>
> **Limitação honesta:** sem Device Owner, os botões Home e Recentes continuam funcionando. Mas
> como a janela fica por cima de tudo, voltar à tela inicial não serve de nada: não há como tocar
> em nenhum app por baixo.

---

## Compilar

Requisitos: **JDK 17** e **Android SDK** (API 34 + build-tools 34.0.0).

O `gradlew` está versionado, então **não é preciso instalar o Gradle** — ele se baixa sozinho na
primeira execução. Basta o JDK e o SDK.

### Ambiente já preparado nesta máquina de TI

| Item | Caminho |
|---|---|
| JDK 17 (Temurin) | `C:\Program Files\Eclipse Adoptium\jdk-17.0.19.10-hotspot` |
| Android SDK | `C:\Android\Sdk` |
| Gradle 8.9 (avulso, opcional) | `C:\Gradle\gradle-8.9` |

### Preparar uma máquina nova

```powershell
# 1. Android command line tools
Invoke-WebRequest 'https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip' -OutFile 'C:\Android\cmdline.zip'
Expand-Archive 'C:\Android\cmdline.zip' 'C:\Android\Sdk\cmdline-tools\tmp'
Move-Item 'C:\Android\Sdk\cmdline-tools\tmp\cmdline-tools' 'C:\Android\Sdk\cmdline-tools\latest'
```

```powershell
# 2. Aceitar as licencas (interativo, responda y) e instalar os pacotes
C:\Android\Sdk\cmdline-tools\latest\bin\sdkmanager.bat --licenses
C:\Android\Sdk\cmdline-tools\latest\bin\sdkmanager.bat "platform-tools" "platforms;android-34" "build-tools;34.0.0"
```

Configure o ambiente em `local.properties` (fora do Git, no mesmo nível de `settings.gradle.kts`):

```properties
sdk.dir=C\:\\Android\\Sdk
tracker.backendBaseUrl=https://tracker.foodsacademy.com.br
```

> Para testar contra a API local, use o **IP da máquina na rede** (ex.: `http://192.168.0.50:5171`).
> `localhost` no aparelho aponta para o próprio aparelho.

### HTTP em desenvolvimento vs HTTPS em produção

Desde o Android 9 o sistema bloqueia tráfego HTTP sem TLS (`CLEARTEXT communication not
permitted`). Como a API de desenvolvimento roda em `http://<ip>:5171`, o build de **debug** libera
essa exceção por `app/src/debug/AndroidManifest.xml`.

O build de **release não a recebe**: em produção o backend é HTTPS e o token do agente não pode
trafegar em texto claro. Por isso o `assembleRelease` também **falha** se
`tracker.backendBaseUrl` começar com `http://` — senão o APK instalaria normalmente e só quebraria
no primeiro heartbeat, dentro do aparelho, onde ninguém vê o erro.

O **código de instalação não vai no APK**. Ele é digitado pelo técnico na tela de registro, no
aparelho. Assim o mesmo APK serve para toda a frota e, se o arquivo vazar, ele não carrega a chave
da empresa junto. Trocar o código é mudar uma variável de ambiente no servidor — sem recompilar
nem reinstalar nada.

### Compilar

```powershell
cd agents\Tracker.Agent.Android; .\gradlew.bat :app:assembleDebug
```

Saída: `app\build\outputs\apk\debug\app-debug.apk` (~8,5 MB). Já vem assinado com a chave de
debug do Android, então **instala direto** em aparelho de teste — é o caminho para validar o
agente antes de existir keystore de produção.

Instalar no aparelho conectado por USB:

```powershell
C:\Android\Sdk\platform-tools\adb.exe install -r app\build\outputs\apk\debug\app-debug.apk
```

### Gerar o APK de release (distribuição na frota)

**Caminho recomendado — pelo CI, sem ter a keystore na máquina.** Serve para qualquer pessoa da
equipe com acesso ao repositório:

1. Aba **Actions** → workflow **APK Agente Android** → **Run workflow**.
2. No fim, o resumo do run mostra o `versionCode` e o **SHA-256**, e o APK fica disponível como
   artefato para baixar.
3. Publique esse APK na página **Agente** do painel (pede a senha do ADM) — a OTA distribui.

A chave da frota nunca sai do cofre de secrets: o workflow a decodifica num arquivo temporário
**fora do workspace**, assina e apaga em seguida, mesmo se o build falhar. As senhas vão por
variável de ambiente (`ORG_GRADLE_PROJECT_*`), não por `-P`, porque argumento de linha de comando
aparece na lista de processos para qualquer usuário logado no servidor.

> **Por que não distribuir a chave para a equipe.** Um APK assinado com chave diferente é recusado
> pelo Android nos aparelhos já instalados e duplica os registros do inventário — a saída seria
> reinstalar aparelho por aparelho. Centralizar a assinatura no CI é o que permite que qualquer
> pessoa gere o APK **sem** que a chave se espalhe por várias máquinas.

#### Configurar os secrets (uma vez só)

Quem tem a keystore roda isto uma única vez, na máquina onde ela está, com o
[gh](https://cli.github.com) autenticado. O script lê as credenciais de um `local.properties` que
já as tenha e envia os quatro secrets — **você não digita nem vê senha nenhuma**:

```powershell
.\deploy\configurar-secrets-android.ps1 -LocalProperties caminho\para\local.properties
```

Antes de enviar, ele **abre a keystore com a senha** para conferir que ela está certa: falhar aqui
é muito melhor que descobrir no CI, onde o sintoma seria um build quebrado sem pista da causa. Use
`-Simular` para conferir tudo sem enviar nada. Os valores vão pela entrada padrão do `gh`, nunca
pela linha de comando — argumento de comando aparece no `tasklist` para qualquer usuário logado.

> Se o `gh auth status` reclamar de outra conta, não é problema: ele devolve erro quando **qualquer**
> login antigo na máquina está com token inválido, mesmo com a conta ativa perfeita. O script checa
> a conta ativa com `gh api user`, que é a que grava o secret.

Depois disso a keystore continua guardada fora do repositório, com backup — ela segue sendo
insubstituível, e o secret é uma cópia de trabalho, não o backup.

**Requisitos no runner:** JDK 17 e Android SDK (API 34 + build-tools 34.0.0) **pré-instalados**. O
egress da rede bloqueia os CDNs de download, então `setup-java`/`setup-android` não funcionam ali
(mesmo motivo do deploy de produção). O workflow confere no início e falha com a instrução se
faltar algum. No runner não existe `local.properties`, então o SDK é localizado por `ANDROID_HOME`.

---

**Com a keystore na máquina** — um comando que faz tudo (cria a keystore na primeira vez, grava
as credenciais de assinatura no `local.properties`, compila e mostra o que digitar no painel):

```powershell
.\deploy\gerar-apk-agente.ps1
```

Ele pede a senha da keystore no prompt (a senha não passa pela linha de comando, que seria
visível na lista de processos) e, ao final, imprime o caminho do APK, o `versionCode`, o
`versionName` e o SHA-256 para conferência. Da segunda vez em diante ele só pede a senha e
recompila. No `local.properties` ele atualiza **só** as quatro chaves de assinatura
(`tracker.keystoreFile`, `tracker.keystorePassword`, `tracker.keyAlias`, `tracker.keyPassword`)
e grava `sdk.dir` apenas se faltar; o resto do arquivo — em especial `tracker.backendBaseUrl` —
é preservado. Os valores são escapados no formato `.properties` (barra invertida e caracteres
especiais) e o arquivo é gravado sem BOM. A keystore fica por padrão em `Documents\TrackerKeystore\` — **fora do repositório**,
de propósito: worktrees e clones novos não levam a chave junto.

O passo a passo manual abaixo continua válido se você preferir controlar cada etapa.

<details>
<summary>Processo manual</summary>

Exige um keystore próprio. Gere uma vez, com o `keytool` que já vem no JDK — a senha é digitada
por você, no prompt:

```powershell
& "$env:JAVA_HOME\bin\keytool.exe" -genkeypair -v -keystore agents\Tracker.Agent.Android\tracker-release.jks -alias tracker -keyalg RSA -keysize 4096 -validity 10000
```

> **Guarde o `.jks` e a senha em local seguro e com backup.** Perdê-los significa não poder mais
> atualizar o app nos aparelhos já instalados: o Android recusa uma atualização assinada com
> chave diferente, e a única saída seria desinstalar e reinstalar em cada aparelho — o que, em
> aparelho bloqueado com Device Owner, é trabalho manual em cada unidade.

Depois preencha em `local.properties` e compile:

```properties
tracker.keystoreFile=tracker-release.jks
tracker.keystorePassword=SUA_SENHA
tracker.keyAlias=tracker
tracker.keyPassword=SUA_SENHA
```

```powershell
.\gradlew.bat :app:assembleRelease
```

Sem o keystore configurado, o `assembleRelease` **falha de propósito**, com a mensagem dizendo o
que falta — em vez de gerar um APK sem assinatura que não instalaria em aparelho nenhum.

</details>

> **O build de release liga o R8 (minify).** As regras em `app/proguard-rules.pro` preservam
> `dados.**` (o Gson do SignalR lê os campos por nome) e o `TrackerDeviceAdminReceiver`. Os
> receivers declarados no manifest — incluindo o `ResultadoInstalacaoReceiver` da OTA — são
> mantidos pelo próprio AGP. Ao criar um DTO novo **fora** do pacote `dados`, ou uma classe
> resolvida por reflexão, acrescente a regra: o sintoma de esquecer é o comando chegar vazio e ser
> descartado em silêncio, sem erro no log.

---

## Instalar num aparelho já em uso (Device Admin)

1. Instale o APK (`adb install app-release.apk`, ou copie e abra no aparelho).
2. Abra o app **Tracker Nutrihouse**. A tela mostra um diagnóstico com o que falta.
3. Digite o **código de instalação** e toque em **Registrar aparelho**. O aparelho aparece no
   inventário como tablet ou celular — o agente detecta pelo tamanho de tela. Código errado mostra
   "Código de instalação incorreto" no próprio campo; falha de rede mostra outra mensagem, para o
   técnico saber onde está o problema.

   Depois do registro o campo e o botão desaparecem: o código **não fica guardado no aparelho**.
   Isso é deliberado — um aparelho roubado não carrega a chave da empresa e, se a TI revogar o
   token dele, ele não consegue se recadastrar sozinho. O preço é que, se um token for regenerado
   no painel, alguém precisa digitar o código no aparelho de novo.
4. Conceda as quatro permissões, na ordem da tela:
   - **Administrador do aparelho** — sem ela o bloqueio remoto não funciona.
   - **Localização** — escolha "Permitir todo o tempo", senão não há histórico com o app fechado.
   - **Sobrepor outros apps** — sem ela o Android 10+ não deixa a tela de bloqueio abrir a partir
     do serviço.
   - **Bateria sem restrição** — sem ela o fabricante encerra o agente e o aparelho aparece
     offline no painel.
5. Confirme no painel: a aba "Agente & Bloqueio" deve mostrar "Administrador ativo" e um
   heartbeat recente.

A tela do app continua servindo de diagnóstico depois da instalação.

---

## Provisionar como Device Owner (proteção máxima)

Só funciona em aparelho **zerado** (recém-tirado da caixa ou após restauração de fábrica) e
**sem nenhuma conta Google adicionada**.

1. Publique o APK numa URL acessível pelo aparelho.
2. Calcule o hash do APK:

```bash
openssl dgst -binary -sha256 app-release.apk | openssl base64 | tr '+/' '-_' | tr -d '='
```

3. Monte o JSON do QR (o checksum é o valor do passo 2):

```json
{
  "android.app.extra.PROVISIONING_DEVICE_ADMIN_COMPONENT_NAME": "br.com.nutrihouse.tracker.agent/br.com.nutrihouse.tracker.agent.admin.TrackerDeviceAdminReceiver",
  "android.app.extra.PROVISIONING_DEVICE_ADMIN_PACKAGE_DOWNLOAD_LOCATION": "https://SEU_SERVIDOR/tracker-agent.apk",
  "android.app.extra.PROVISIONING_DEVICE_ADMIN_SIGNATURE_CHECKSUM": "CHECKSUM_DO_PASSO_2",
  "android.app.extra.PROVISIONING_LEAVE_ALL_SYSTEM_APPS_ENABLED": true,
  "android.app.extra.PROVISIONING_SKIP_ENCRYPTION": false
}
```

4. Gere um QR code com esse JSON.
5. No aparelho zerado, na primeira tela de boas-vindas, **toque 6 vezes** em qualquer ponto vazio.
   O leitor de QR abre. Aponte para o código.
6. O aparelho baixa e instala o agente como Device Owner. O `onProfileProvisioningComplete`
   aplica o endurecimento sozinho: desinstalação bloqueada, restauração de fábrica e modo de
   segurança proibidos.

Alternativa por cabo, em aparelho zerado sem contas:

```bash
adb shell dpm set-device-owner br.com.nutrihouse.tracker.agent/.admin.TrackerDeviceAdminReceiver
```

### Descomissionar um aparelho Device Owner

O endurecimento impede restauração de fábrica. Antes de devolver ou vender o aparelho, remova-o
pelo `adb`:

```bash
adb shell dpm remove-active-admin br.com.nutrihouse.tracker.agent/.admin.TrackerDeviceAdminReceiver
```

---

## Identificação no inventário

### Informe o número de série da etiqueta

A tela de registro pede o **código de instalação** e o **número de série** da etiqueta/caixa do
aparelho (ou de Configurações > Sobre o aparelho). A série é obrigatória quando o Android não a
entrega ao app — ou seja, em qualquer aparelho moderno sem Device Owner.

**Patrimônio e IMEI não são digitados no aparelho** (desde a v1.1.2): ficam a cargo do painel,
onde a TI já mantém o cadastro do ativo. Se o ativo já existe no inventário com a mesma série,
o registro o reencontra; se não existe, ele entra com patrimônio provisório `AUTO-<série>`, que a
TI corrige no painel.

Sem a série digitada, o agente se identificaria por `ANDROID-<ANDROID_ID>`, e **esse valor não é
estável**: o Android o escopa por chave de assinatura do APK, então ele muda sempre que o app é
reinstalado assinado com outra chave (trocar um APK de teste pelo de release faz isso). Quando
ele muda, o mesmo aparelho volta ao inventário como um ativo novo — já duplicou a frota assim.

O reencontro segue a ordem **patrimônio → número de série → IMEI**, e os identificadores só
melhoram: um serial de fábrica nunca é substituído pelo provisório, e um patrimônio real nunca
volta a ser `AUTO-*`.

### O que o Android deixa ler sozinho

| Versão do aparelho | Número de série | IMEI |
|---|---|---|
| Até o Android 7.1 | Automático | Automático |
| Android 8.0 a 9 | Automático (permissão de telefonia, pedida no registro) | Automático (idem) |
| **Android 10 ou mais novo** | **Só com Device Owner** | **Só com Device Owner** |

Do Android 10 em diante não existe permissão que resolva: o sistema reserva esses dados a apps
privilegiados. Em aparelho moderno, ou você provisiona como Device Owner (exige restauração de
fábrica, ver adiante), ou o técnico digita os valores no registro. A tela de diagnóstico mostra
qual é o caso do aparelho: "Série de fábrica" quando conseguiu ler, "Série provisória" quando não.

Com Device Owner, número de série e IMEI reais são preenchidos automaticamente (o IMEI também é
completado depois, via heartbeat, se o cadastro estiver sem ele). A busca do painel encontra o
aparelho por hostname, número de série ou IMEI.

---

## Atualização OTA (sem loja)

O agente se atualiza sozinho, sem Play Store e sem tocar nos aparelhos:

1. Gere o APK novo (`./gradlew :app:assembleRelease`) com `versionCode` **maior** que o atual em
   `app/build.gradle.kts` (obrigatório: o Android recusa instalar versão igual ou menor).
2. No painel, página **Agente** → *Publicar nova versão*: envie o `.apk`, informe o
   `versionCode`/`versionName` exatamente como no `build.gradle.kts` e confirme com a sua senha.
3. Cada aparelho compara o próprio `versionCode` no heartbeat (a cada ~1 min). Estando atrás, ele
   baixa o APK (autenticado pelo token do dispositivo), **confere o SHA-256** e se instala via
   `PackageInstaller`.

Como a instalação se resolve:

| Situação do aparelho | O que acontece |
|---|---|
| Device Owner | Instala em silêncio. Ninguém toca no aparelho. |
| Sem Device Owner, 1ª OTA | O Android mostra o diálogo "Deseja atualizar este app?" — o usuário toca em Atualizar. Requer a permissão "instalar apps desconhecidos" (passo 5 da tela de registro). |
| Sem Device Owner, OTAs seguintes (Android 12+) | Silencioso: após a 1ª OTA o agente vira o instalador de si mesmo. |

O APK precisa estar **assinado com a mesma keystore** do instalado — assinatura diferente é
recusada pelo Android (o aparelho segue funcionando na versão antiga).

Desde a 1.1.3, uma instalação que fica esperando o usuário (`PENDING_USER_ACTION`) sem resposta
é liberada depois de **10 minutos**, e a oferta volta no heartbeat seguinte. Sessões do
`PackageInstaller` que falham na cópia ou no commit são abandonadas, assim como as antigas que
tiverem sobrado. Depois de instalar, o
serviço religa sozinho (`MY_PACKAGE_REPLACED`) e um bloqueio ativo continua valendo: o sinal
persistente sobrevive à atualização.

---

## Definir o código de instalação no servidor

O código digitado no app é comparado com `Enrollment:CompanyKey` do backend. Ele **não** fica em
arquivo versionado.

Desenvolvimento (a partir da raiz do repositório):

```bash
dotnet user-secrets set "Enrollment:CompanyKey" "<CODIGO-DE-DEV>" --project src/Tracker.Api
```

Produção (IIS, variável de ambiente do pool ou do sistema):

```powershell
[Environment]::SetEnvironmentVariable('Enrollment__CompanyKey', '<CODIGO-DO-COFRE>', 'Machine')
```

Reinicie a API depois de definir. Para trocar o código, mude o valor e reinicie — os aparelhos já
registrados continuam funcionando, porque eles usam o token individual, não o código.

> Quem tiver o código consegue cadastrar aparelhos no inventário: guarde-o **só no cofre de
> senhas**, nunca em documento versionado. Use um valor longo (20+ caracteres). Troca e demais
> cuidados: [backup-e-chaves.md](../operacao/backup-e-chaves.md#enrollment__companykey-o-código-de-instalação-dos-agentes).

---

## Diagnóstico

| Sintoma | Causa provável |
|---|---|
| Aparelho aparece offline | Restrição de bateria ativa, ou o fabricante matou o serviço. O `VigilanteWorker` religa a cada 15 min. |
| Bloqueio não abre a tela | Falta a permissão "Sobrepor outros apps". |
| Botão Bloquear não faz nada | Sem permissão de administrador (`NivelProtecao = Nenhuma`). |
| Sem histórico de localização | Localização não está em "Permitir todo o tempo". |
| Alerta "administrador desativado" | O usuário desativou o admin: o agente pode ser desinstalado. Recupere o aparelho. |
| Bloqueia só a tela (pede a senha do usuário e libera) | A sobreposição não subiu. Confira a permissão "Sobrepor outros apps". |
| Bloqueio demora ~1 minuto em vez de ser imediato | O comando está chegando pelo heartbeat, não pelo SignalR. Confira se `AddSignalR().AddJsonProtocol(...)` está no `Program.cs`. |
| "Código de instalação incorreto" | `Enrollment:CompanyKey` no servidor difere do que foi digitado, ou a API não foi reiniciada após definir a variável. |
| `CLEARTEXT communication not permitted` | APK de release apontando para `http://`, ou debug sem o manifest de `src/debug`. Use HTTPS, ou o build de debug. |
| API de dev inacessível pelo celular | A API precisa escutar em `0.0.0.0` (perfil `rede` do launchSettings), a porta 5171 liberada no firewall, e o aparelho no mesmo Wi-Fi. |
| App voltou a pedir o código | O token foi revogado/regenerado no painel. Digite o código novamente no aparelho. |
| Aparelho **bloqueado** que não desbloqueia nunca, sem heartbeat no painel | Token revogado com o aparelho bloqueado: ver [Bloqueado e sem token](#bloqueado-e-sem-token-aparelho-preso). |
| Aparelho parou de reportar posição com a localização negada | Esperado: o serviço sobe como `specialUse` e segue reaplicando o bloqueio; o painel mostra o alerta `LocalizacaoNegada`. |
| Publiquei um APK e a frota não atualiza | `versionCode` publicado igual/menor que o instalado (o painel barra, mas confira), permissão "instalar apps desconhecidos" ausente nos aparelhos sem Device Owner, ou APK assinado com outra keystore. |
| O mesmo aparelho apareceu duas vezes no inventário | Foi registrado sem o patrimônio depois de o APK trocar de assinatura (o identificador provisório muda junto). Arquive o registro novo, apague os dados do app no aparelho e registre de novo **informando o patrimônio** do cadastro original. |
| A tela diz "Série provisória" | Aparelho com Android 10+ sem Device Owner: o sistema não libera o serial de fábrica. Digite o número de série e o patrimônio no registro, ou provisione como Device Owner. |
| Painel mostra "Desatualizados" que nunca zeram | Aparelhos com agente 1.0.0 (anterior à OTA) não sabem se atualizar: essa primeira troca é manual, por `adb install -r` ou pelo APK aberto no aparelho. |

Logs no aparelho:

```bash
adb logcat -s AgenteService:V Bloqueio:V TrackerAdmin:V BackendClient:V
```

**Alguns aparelhos não expõem os logs do app** (visto num Lenovo Tab P11 Pro: o `logcat` filtrado
volta vazio mesmo com o agente rodando). Nesses casos, diagnostique pelo estado do sistema.

Bloqueio ativo — a janela com `appop=SYSTEM_ALERT_WINDOW` é a sobreposição:

```bash
adb shell dumpsys window windows | Select-String nutrihouse
```

Canal em tempo real conectado — `1433` é 5171 em hexadecimal, e `01` é `ESTABLISHED`:

```bash
adb shell "cat /proc/net/tcp /proc/net/tcp6" | Select-String ":1433 01 "
```

Serviço vivo e restrições de bateria:

```bash
adb shell dumpsys activity services | Select-String nutrihouse
```

### Bloqueado e sem token: aparelho preso

Aparelho **bloqueado** cujo token foi revogado (token regenerado no painel, backup antigo do
banco, troca de servidor) fica bloqueado para sempre: o bloqueio não depende do token, o
Desbloquear não chega (sem token não há heartbeat nem hub) e a sobreposição cobre a tela de
registro. Pendência registrada no
[item 27 do backlog](../desenvolvimento/melhorias-pendentes.md#27-android-bloqueado-com-token-revogado-fica-preso).

**Prevenção:** não gere token novo para um celular/tablet bloqueado.

**Saída hoje** (com o aparelho na mão da TI, depuração USB ligada):

1. No painel, emita **Desbloquear** para o aparelho. Ao se registrar de novo, ele recebe o estado
   do **último comando**; sem isso, volta a travar logo depois do registro.
2. Apague os dados do app (token, sinal de bloqueio e demais estados):

   ```bash
   adb shell pm clear br.com.nutrihouse.tracker.agent
   ```

3. Abra o app e refaça o registro (código de instalação + a **mesma** série da etiqueta, para
   reencontrar o cadastro original).

> Caminho sugerido a partir do código, **não testado nesta revisão**. Se o Android recusar o
> `pm clear` (o esperado em aparelho **Device Owner**), use o procedimento de
> [descomissionamento](#descomissionar-um-aparelho-device-owner) ou, em Device Admin, desative o
> administrador à mão (Configurações > Segurança > Apps de administração do dispositivo),
> desinstale e reinstale o APK.

---

## Mudanças no backend que acompanham este agente

- `TipoDispositivo.Telefone` renomeado para `Mobile` (mesmo valor 1), cobrindo celulares e
  tablets num único tipo, com `Imei` opcional. Migração `UnificaTelefoneETabletEmMobile`.
- `DispositivoComAgente`: `Fabricante`, `Modelo`, `BateriaPercentual`, `NivelProtecao`.
- Entidade `AlertaDispositivo` e `POST /api/agente/alertas`, mais os endpoints ADM
  `GET /api/dispositivos/{id}/alertas` e `POST .../alertas/{alertaId}/reconhecer`.
- `HeartbeatResponse.DeveEstarBloqueado`, para o agente recuperar o bloqueio perdido. Desde
  24/09/2026 ele segue o último comando Bloquear/Desbloquear emitido, não só o status.
- `POST /api/dispositivos/mobiles` para cadastro manual (`/telefones` segue como alias).
- Migração `AgenteMobileTabletEAlertas` (telemetria e alertas).
