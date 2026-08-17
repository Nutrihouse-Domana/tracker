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
   Mostra o patrimônio e a série, para a TI identificar o aparelho sem liberá-lo.
2. **`lockNow()` uma única vez** ao aplicar o bloqueio, só para apagar a tela na hora e tirar o
   aparelho da mão do usuário. Não é repetido: em ciclo, daria a impressão de "só travou a tela".
3. **Modo quiosque** (`startLockTask`) quando há Device Owner, que fecha também o botão Home.

O bloqueio é **persistente**, como no Windows: fica gravado localmente
(`Armazenamento.bloqueado`, em `EncryptedSharedPreferences`) e é reaplicado pelo `BootReceiver`
após reiniciar, mesmo offline. Só a TI o remove, pelo botão Desbloquear.

Se o app for reinstalado ou os dados limpos, o sinal local se perde — nesse caso o campo
`deveEstarBloqueado` do heartbeat traz o estado do servidor e o agente **reaplica a trava** e
registra o alerta `BloqueioReaplicado`.

### Wi-Fi na tela de bloqueio

Um aparelho bloqueado e sem rede **nunca receberia o comando de Desbloquear** — ficaria travado
para sempre. Por isso a tela tem um botão de Wi-Fi, igual à do agente Windows. O comportamento
muda conforme o nível de proteção, porque o Android 10 tirou dos apps comuns a capacidade de
conectar a uma rede informando SSID e senha:

| | **Device Owner** | **Device Admin** |
|---|---|---|
| Como conecta | Lista de redes, senha e botão Conectar **dentro** da tela de bloqueio | Botão abre o **painel de Wi-Fi do sistema** |
| Sai da tela de bloqueio? | Não | Sim, por até **60 segundos** |

No modo Device Admin, o painel usado é o `Settings.Panel.ACTION_WIFI` (Android 10+), que mostra
**apenas** a lista de redes — não dá acesso ao resto das Configurações, então o usuário não
consegue usá-lo para desativar o agente. Durante esses 60 segundos a sobreposição sai da frente
(senão o painel ficaria inacessível) e o aparelho fica utilizável: é uma brecha consciente, e o
`AgenteService` recoloca a janela assim que o tempo expira. O bloqueio em si nunca é removido —
o sinal local continua ligado o tempo todo.

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

Sem Device Owner, o Android não entrega o número de série nem o IMEI a um app comum. Por isso a
tela de registro tem dois campos opcionais que o técnico preenche na instalação:

- **Número de série** — o da etiqueta/caixa do aparelho. Vira o `NumeroSerie` do inventário.
- **IMEI** — visível discando `*#06#`. Além de identificar, ele **religa** o registro a um
  aparelho que a TI já tenha cadastrado antes pelo IMEI (evita duplicar o ativo).

Se os campos ficarem vazios, o agente usa o `ANDROID_ID`, gravado como `ANDROID-<id>` no campo
Número de série. Ele é estável por aparelho e só muda em restauração de fábrica — serve como
chave de inventário, mas **não** é o serial da etiqueta. Ajuste o patrimônio (`AssetTag`)
manualmente no painel; o auto-cadastro grava `AUTO-<serie>` como provisório.

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
recusada pelo Android (o aparelho segue funcionando na versão antiga). Depois de instalar, o
serviço religa sozinho (`MY_PACKAGE_REPLACED`) e um bloqueio ativo continua valendo: o sinal
persistente sobrevive à atualização.

---

## Definir o código de instalação no servidor

O código digitado no app é comparado com `Enrollment:CompanyKey` do backend. Ele **não** fica em
arquivo versionado.

Desenvolvimento (a partir da raiz do repositório):

```bash
dotnet user-secrets set "Enrollment:CompanyKey" "Tr4cker" --project src/Tracker.Api
```

Produção (IIS, variável de ambiente do pool ou do sistema):

```powershell
[Environment]::SetEnvironmentVariable('Enrollment__CompanyKey', 'Tr4cker', 'Machine')
```

Reinicie a API depois de definir. Para trocar o código, mude o valor e reinicie — os aparelhos já
registrados continuam funcionando, porque eles usam o token individual, não o código.

> O código atual (`Tr4cker`) é curto. Quem o tiver consegue cadastrar aparelhos no inventário.
> Como trocá-lo não exige recompilar o APK, vale considerar algo mais longo quando a frota crescer.

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
| Publiquei um APK e a frota não atualiza | `versionCode` publicado igual/menor que o instalado (o painel barra, mas confira), permissão "instalar apps desconhecidos" ausente nos aparelhos sem Device Owner, ou APK assinado com outra keystore. |
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

---

## Mudanças no backend que acompanham este agente

- `TipoDispositivo.Telefone` renomeado para `Mobile` (mesmo valor 1), cobrindo celulares e
  tablets num único tipo, com `Imei` opcional. Migração `UnificaTelefoneETabletEmMobile`.
- `DispositivoComAgente`: `Fabricante`, `Modelo`, `BateriaPercentual`, `NivelProtecao`.
- Entidade `AlertaDispositivo` e `POST /api/agente/alertas`, mais os endpoints ADM
  `GET /api/dispositivos/{id}/alertas` e `POST .../alertas/{alertaId}/reconhecer`.
- `HeartbeatResponse.DeveEstarBloqueado`, para o agente recuperar o bloqueio perdido.
- `POST /api/dispositivos/mobiles` para cadastro manual (`/telefones` segue como alias).
- Migração `AgenteMobileTabletEAlertas` (telemetria e alertas).
