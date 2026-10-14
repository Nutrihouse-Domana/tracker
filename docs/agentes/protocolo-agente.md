# Protocolo servidor ↔ agente

Contrato entre o backend do Tracker e os agentes (Windows e Android): como o agente se cadastra,
se autentica, reporta o estado e recebe comandos. Serve para diagnosticar a frota, para mexer num
agente sem quebrar o outro e para escrever um agente novo.

Referências principais: `src/Tracker.Api/Controllers/AgenteController.cs` (endpoints),
`src/Tracker.Application/Dtos/AgenteDtos.cs` (DTOs), `src/Tracker.Api/Agente/` (autenticação e
hub). Levantado em 23/09/2026 e revisado em 24/09/2026 (revisão completa do código): ao mudar
o protocolo, **atualize este documento**.

---

## Visão geral

```
                 1x  POST /api/agente/auto-enrollment  (X-Enrollment-Key)  -> token
Agente  ───────────────────────────────────────────────────────────────────────────►  API
   │      a cada 60 s  POST /api/agente/heartbeat     (X-Agent-Token)    -> comandos, bloqueio, OTA
   │      tempo real   WS  /hubs/comandos?token=...   <- "ExecutarComando"
   │      ao executar  POST /api/agente/comandos/{id}/confirmar
   │      quando há     POST /api/agente/alertas   ·   GET /api/agente/atualizacao/apk (só Android)
```

- **Dois caminhos de entrega de comando:** SignalR (imediato) e heartbeat (rede de segurança).
  Um comando só sai da fila quando o agente o **confirma**.
- O painel nunca fala direto com o agente: ele grava o comando no banco e o servidor entrega.

---

## Formato

- JSON em **camelCase**.
- **Enums como texto** (`"Bloquear"`, `"Mobile"`), tanto nos controllers quanto no SignalR. A
  configuração fica num lugar só, `JsonConfig.Aplicar` (`src/Tracker.Api/Json/JsonConfig.cs`),
  aplicada aos controllers, ao `AddSignalR().AddJsonProtocol(...)` e aos testes. Sem a do
  SignalR, o hub mandava números e os agentes descartavam o comando em silêncio. **Os dois
  agentes aceitam `tipoComando` como texto ou como número**, no heartbeat e no hub, como defesa.
- Datas da API saem em UTC com `Z` — no SignalR também (`UtcDateTimeConverter`, via `JsonConfig`).
  Os payloads do agente não trazem datas.
- **Validação:** o auto-cadastro (`AutoEnrollmentRequest`) e os alertas (`AlertaAgenteRequest`)
  têm validador FluentValidation (400 com `ValidationProblemDetails`). O heartbeat **não** é
  recusado por enum desconhecido: `nivelProtecao` ou `origem` fora do enum são **ignorados**
  (recusar cortaria também a entrega de comandos de bloqueio). Campos `string` não anuláveis
  continuam obrigatórios pela validação implícita do `[ApiController]`.

---

## Autenticação

| Header | Usado em | Validação |
|---|---|---|
| `X-Enrollment-Key` | Só no auto-cadastro | Igual a `Enrollment:CompanyKey` (comparação em tempo constante). Chave vazia no servidor recusa tudo |
| `X-Agent-Token` | Todos os demais endpoints | O servidor calcula o SHA-256 e procura em `TokenAgenteHash` de `Notebook` e `Mobile` (`AgentAuth.ResolverAsync`) |
| `?token=` (query) | Conexão SignalR | Mesmo `AgentAuth`. Token inválido faz `Context.Abort()` |

- **Token:** 32 bytes aleatórios em base64url (~43 caracteres). O banco guarda só o hash SHA-256.
- **Cada cadastro gera token novo e invalida o anterior.** O painel também regenera (*Gerar token
  do agente*, `POST /api/dispositivos/{id}/enrollment`), e isso derruba o agente que usava o antigo.
- **Dispositivo arquivado:** o filtro global o esconde, o token não resolve e o agente recebe 401.
- Ao criar um **tipo novo com agente**, inclua-o em `AgentAuth.ResolverAsync` e em
  `BuscaController.BuscarDispositivos` (ver `CLAUDE.md`).

---

## Endpoints

| Método e rota | Auth | Sucesso | Erros |
|---|---|---|---|
| `POST /api/agente/auto-enrollment` | `X-Enrollment-Key` | 200 `EnrollmentResponse` | 401 "Chave de instalação inválida." · 400 validação (ex.: serial genérico de BIOS **sem** patrimônio) · 400 "Auto-cadastro só vale para notebook ou mobile." · 409 existe ativo **sem agente** (periférico) com a identificação · 409 "…está arquivado. Restaure-o no painel…" |
| `POST /api/agente/heartbeat` | `X-Agent-Token` | 200 `HeartbeatResponse` | 401 (sem corpo) |
| `POST /api/agente/comandos/{id}/confirmar` | `X-Agent-Token` | 204 | 401 · 404 (comando inexistente ou de outro aparelho) |
| `POST /api/agente/alertas` | `X-Agent-Token` | 204 | 401 · 400 (tipo fora do enum) |
| `GET /api/agente/atualizacao/apk` | `X-Agent-Token` | 200 APK (`application/vnd.android.package-archive`) | 401 · 404 "Nenhum pacote publicado." |
| `WS /hubs/comandos?token=` | query | conexão no grupo `device-{id}` | conexão abortada |
| `GET /health` | nenhuma | `{"status":"ok"}` | usado pelo deploy, não pelos agentes |

---

## 1. Auto-cadastro

**Request** (`AutoEnrollmentRequest`):

| Campo | Tipo | Obrigatório | Windows envia | Android envia |
|---|---|---|---|---|
| `hostname` | string | sim | `MachineName` | nome do aparelho (definido pelo usuário) |
| `numeroSerie` | string | sim | serial da BIOS (WMI); se for genérico, `WIN-<UUID do SMBIOS>`, senão `WIN-<MachineGuid>`, senão `HOST-<nome>` | série **digitada** pelo técnico, senão a de fábrica, senão `ANDROID-<ANDROID_ID>` |
| `sistemaOperacional` | string | sim | descrição do SO | `"Android"` |
| `versaoSO` | string? | | versão | `"Android 13 (API 33)"` |
| `macAddress` | string? | | 1ª interface ativa | `null` |
| `tipo` | enum | sim | `"Notebook"` (config `Tipo`) | `"Mobile"` |
| `imei` | string? | | `null` | só com Device Owner |
| `fabricante`, `modelo` | string? | | — | sim |
| `nivelProtecao` | enum? | | — | `Nenhuma` / `DeviceAdmin` / `DeviceOwner` |
| `assetTag` | string? | | — | o cliente aceita, mas **a tela de registro não pede** (patrimônio fica no painel) |

**Serial genérico de BIOS.** Placas genéricas e VMs devolvem o mesmo texto ("To be filled by
O.E.M.", "Default string", "0", só um caractere repetido…) em máquinas diferentes. Usado como
identidade, faria uma máquina reencontrar o cadastro de outra e roubar o token dela. A regra
fica em `SerialGenerico.EhGenerico` (`src/Tracker.Application/Common/SerialGenerico.cs`), com
**espelho** em `agents/Tracker.Agent.Windows/InfoMaquina.cs` — mudar a lista exige mexer nos dois:

- **Sem patrimônio:** o validador recusa com **400**.
- **Com patrimônio:** o serial recebido é **descartado** — não entra no reencontro nem na
  promoção — e um cadastro novo grava `SEM-SERIE-<patrimônio>` (único, porque o patrimônio é).
- O agente Windows já não manda serial genérico: troca-o por `WIN-<UUID do SMBIOS>` →
  `WIN-<MachineGuid>` → `HOST-<nome>`. O `WIN-*` é estável por máquina; o `HOST-*` muda se a
  máquina for renomeada. Máquina cadastrada antes com serial genérico ou `HOST-*` pode ganhar
  registro **novo** se se recadastrar ([item 28](../desenvolvimento/melhorias-pendentes.md#28-máquinas-windows-com-serial-genérico-duplicam-ao-recadastrar)).

**O que o servidor faz:**

1. **Reencontra** um cadastro existente, nesta ordem: **patrimônio → número de série → IMEI**
   (IMEI só entre `Mobile`; série genérica é pulada). Arquivados ficam de fora.
2. Se não achou, mas existe um **arquivado** com a mesma série ou patrimônio, responde **409**.
3. Se não achou nada, **cria** o `Notebook`/`Mobile` com `Status = EmEstoque` e patrimônio
   `AUTO-<série>` quando não veio um.
4. Se achou, **promove** identificadores sem nunca rebaixar a qualidade: serial de fábrica nunca
   vira `ANDROID-*` e patrimônio real nunca volta a `AUTO-*`. As duas checagens consideram também
   os arquivados.
5. Sobrescreve hostname, SO, versão e MAC; completa IMEI, fabricante, modelo e proteção quando
   vierem.
6. Gera o token e responde:

```json
{ "dispositivoId": "3f2c…", "token": "q8Xz…", "assetTag": "AUTO-5CD1234XYZ" }
```

**Onde o agente guarda o token:**

- **Windows:** `token.dat`, ao lado do executável, em texto puro. Para obter o token, o agente
  tenta nesta ordem: `token.dat` → `AgentToken` do `appsettings.json` → auto-cadastro.
- **Android:** `EncryptedSharedPreferences`. O código de instalação **não é guardado**.

---

## 2. Heartbeat (a cada 60 s)

**Request** (`HeartbeatRequest`). Todos os campos são opcionais.

| Campo | Windows | Android | Efeito no servidor |
|---|---|---|---|
| `hostname` | sim | sim | **Sobrescreve** o hostname (por isso existe a Identificação interna) |
| `agenteVersao` | `InformationalVersion` do assembly (tag da release, carimbada pelo `gerar-pendrive.ps1`; `0.0.0-dev` em build local) | `versionName` | Atualiza a versão exibida |
| `agenteVersaoCode` | — | `versionCode` | Atualiza a versão; **sem esse campo não há oferta OTA** |
| `latitude`, `longitude` | por IP | GPS/rede | Com os dois, **insere uma linha** em `HistoricoLocalizacoes` |
| `enderecoAproximado` | "cidade, região, país" | `null` | Gravado com o ponto |
| `precisaoMetros` | `5000` fixo | precisão real | Gravado com o ponto |
| `origem` | — | `"GpsAndroid"` (sempre) | Sem ela (ou fora do enum), o servidor assume `IpGeolocationWindows` |
| `bateriaPercentual` | — | sim | Atualiza |
| `nivelProtecao` | — | sim | Atualiza; **se caiu**, cria o alerta `ProtecaoRebaixada`. Valor fora do enum é ignorado |
| `imei` | — | com Device Owner | Grava **só se o cadastro estiver vazio** (nunca sobrescreve o digitado) |

O heartbeat grava `UltimoHeartbeatEm` e **não altera o status** do dispositivo.

**Sem data da leitura da localização:** o ponto é gravado com a hora do heartbeat. O Android
1.1.3 descarta a última posição conhecida com mais de 5 min, mas não há campo para dizer quando
a posição foi lida ([item 30](../desenvolvimento/melhorias-pendentes.md#30-heartbeat-sem-a-data-da-leitura-da-localização)).

**Response** (`HeartbeatResponse`):

```json
{
  "comandosPendentes": [ { "id": "9b1e…", "tipoComando": "Bloquear" } ],
  "deveEstarBloqueado": true,
  "atualizacaoAgente": { "versaoCode": 5, "versaoNome": "1.1.3",
                         "hashSha256": "AB12…", "tamanhoBytes": 6291456 }
}
```

- `comandosPendentes`: comandos em `Pendente` **e** `Enviado`, em ordem de emissão. Os
  `Pendente` passam a `Enviado`. Um comando é **reentregue a cada heartbeat até ser confirmado**,
  então o agente precisa deduplicar pelo `id`.
- `deveEstarBloqueado`: segue o **último comando** `Bloquear`/`Desbloquear` que não falhou
  (pendente, enviado ou confirmado; o mais recente por `DataEmissao`). Último = `Bloquear` →
  `true`; último = `Desbloquear` → `false`. **Só sem comando algum** vale o status
  (`Bloqueado` → `true`). Regra em `RegrasDispositivo.DeveEstarBloqueado`
  (`src/Tracker.Application/Dispositivos/`) + `ConsultasComandoRemoto.UltimoComandoVigenteAsync`.
  Antes, o campo vinha só do status, que só vira `Bloqueado` quando o agente confirma: o
  heartbeat que entregava um Bloquear pendente respondia "desbloqueado" e o Android destravava
  logo depois de aplicar.
  - O **Android** usa o campo para reaplicar o bloqueio se o sinal local sumir, e **ignora** o
    `deveEstarBloqueado` de um heartbeat que **saiu antes** de um comando ser aplicado (contador
    `geracaoComandos` em `AgenteService`): a resposta já nasceu velha e desfaria o comando
    recém-chegado pelo hub. Comando entregue no próprio ciclo prevalece.
  - O **Windows** faz o mesmo desde 13/10/2026 (`ExecutorDeComandos.SincronizarBloqueioAsync`,
    contador `_geracaoComandos`): recria o `locked.flag` e reporta `BloqueioReaplicado` quando o
    servidor diz bloqueado e o sinal sumiu; remove a trava quando o servidor diz desbloqueado e o
    sinal ficou. Com `Simular=true` só registra no log. Antes ele ignorava o campo (item 22).
- `atualizacaoAgente`: o pacote de maior `versaoCode`, quando ele é maior que o
  `agenteVersaoCode` enviado; senão `null`. **O Windows ignora esse campo** (não tem OTA).

**401 no heartbeat:**

- **Windows:** apaga o `token.dat`, refaz o auto-cadastro com a chave da empresa e reconecta o hub.
- **Android:** apaga o token e **aguarda um novo registro manual**, porque não tem o código
  guardado ([item 23](../desenvolvimento/melhorias-pendentes.md#23-agente-android-não-se-recadastra-sozinho)).
  O 401 **não mexe no bloqueio**: aparelho bloqueado continua bloqueado, e hoje só sai por `adb`
  ([item 27](../desenvolvimento/melhorias-pendentes.md#27-android-bloqueado-com-token-revogado-fica-preso)).

---

## 3. SignalR: `/hubs/comandos`

- **Conexão:** `{BackendBaseUrl}/hubs/comandos?token=<token>`. O servidor põe a conexão no grupo
  `device-{dispositivoId}`.
- **Único evento (servidor → agente):** `ExecutarComando`, com o payload
  `{ "id": "…", "tipoComando": "Bloquear" }`. É enviado logo depois que o ADM emite o comando no
  painel.
- **O hub não tem métodos agente → servidor.** Confirmação e alertas vão por REST.
- **O envio pelo hub não muda o status do comando.** Ele só vira `Enviado` no heartbeat.
- **Reconexão:**
  - **Windows:** reconexão **perpétua**: `WithAutomaticReconnect` com política própria
    (`RecuoHub`: 0, 2, 5, 10, 30 e depois 60 s para sempre) mais um laço no `Worker` que refaz o
    `StartAsync` quando a conexão está `Disconnected` (primeiro start falho, evento `Closed`).
    Ao trocar o token no recadastro, a conexão é recriada com o token novo. Enquanto o hub
    está fora, o heartbeat continua entregando os comandos.
  - **Android:** o cliente Java **não tem** reconexão automática. `AgenteService.lacoDeConexaoHub`
    verifica a cada 5 s e reconecta com espera crescente, até 60 s. Ao reconectar, o agente manda
    um heartbeat extra (sem localização) para buscar pendências.

---

## 4. Comandos remotos

**Tipos** (`TipoComandoRemoto`): `Bloquear`, `Desbloquear`.

**Ciclo de status** (`StatusComandoRemoto`):

```
Pendente ──(heartbeat entrega)──► Enviado ──(POST .../confirmar)──► Confirmado
```

- **`Falhou` existe no enum, mas nada o grava.** Um comando nunca confirmado fica em
  `Pendente`/`Enviado` para sempre ([item 11](../desenvolvimento/melhorias-pendentes.md#11-status-de-comando-falhou-nunca-é-gravado)).
- **Efeito da confirmação no dispositivo:**
  - `Bloquear` → status `Bloqueado`.
  - `Desbloquear` → **só mexe se o status era `Bloqueado`**: volta a `EmUso` se houver
    responsável (detentor, ou unidade no ativo do local), senão `EmEstoque`. `Baixado` e
    `EmManutencao` ficam como estão (`RegrasDispositivo.StatusAposDesbloqueio`).
- **No painel**, `Bloqueado` só entra e sai por comando: `AtualizarStatus` recusa `Bloqueado`
  manual e qualquer mudança num aparelho `Bloqueado` (400); abrir e concluir reparo idem.
  Arquivar/excluir é recusado com o aparelho `Bloqueado` **ou** com um `Bloquear` ainda a
  caminho (último comando = Bloquear pendente/enviado).
- `POST /api/dispositivos/{id}/comandos` valida o `tipoComando` (enum): um valor fora do enum
  era gravado e auditado como "Desbloqueou".
- **Agente Windows:** deduplica por `id`, executa e confirma. Se falhar, esquece o `id` para
  tentar de novo no próximo ciclo.
- **Agente Android:** deduplica por `id` (lembra os **últimos 50**): a reentrega do mesmo comando
  (hub + heartbeat) só é reconfirmada, não reaplicada. Um tipo desconhecido **não** é confirmado.

**O bloqueio é persistente no aparelho**, independentemente do servidor:

- **Windows:** arquivo-sinal `%ProgramData%\TrackerAgent\locked.flag`. Um timer de 2 s reabre a
  tela cheia (`LockOverlay`, uma janela por monitor, lançada na sessão do usuário pela tarefa
  agendada `TrackerAgentLock`). O serviço sabe que a tela está viva pelo **processo**
  `--overlay` em cada sessão ativa (WMI), e o overlay segura o mutex `Local\TrackerAgentOverlay`
  (uma tela por sessão). Se a tela não sobe, o relançamento recua de 10 s até 120 s por sessão.
- **Android:** sobreposição `TYPE_APPLICATION_OVERLAY`, reaplicada a cada 2 s, a partir do sinal
  `bloqueado` persistido — **independente do token**. Com Device Owner, a `BloqueioActivity`
  roda em lock task por baixo da sobreposição.

---

## 5. Alertas

`POST /api/agente/alertas` com `{ "tipo": "...", "detalhe": "..." }`. Tipos
(`TipoAlertaDispositivo`):

| Tipo | Quem gera | Quando |
|---|---|---|
| `AdminDesativado` | Agente | O usuário abriu ou concluiu a desativação do administrador do dispositivo. O servidor também grava `NivelProtecao = Nenhuma` |
| `LocalizacaoNegada` | Agente | Permissão de localização negada ou revogada (uma vez por execução do serviço) |
| `BloqueioReaplicado` | Agente (Android e Windows) | `deveEstarBloqueado` veio `true`, mas o sinal local tinha sumido |
| `ProtecaoRebaixada` | **Servidor** | O heartbeat trouxe um `nivelProtecao` menor que o anterior |
| `ImpressoraOffline` | **Servidor (SNMP)** | Impressora monitorada deixou de responder (não é de agente) |
| `SuprimentoBaixo` | **Servidor (SNMP)** | Toner/tinta/cilindro abaixo do limite (não é de agente) |

O painel mostra os alertas em *Agente & Bloqueio* (e, na impressora, na aba dela), e **Tratar**
os marca como reconhecidos. Tratar alerta de agente pede `dispositivos.bloquear`; de impressora,
`dispositivos.cadastrar`.

> O validador de `POST /api/agente/alertas` só confere se o tipo existe no enum: hoje um agente
> consegue postar `ImpressoraOffline`/`SuprimentoBaixo` (item 34 de
> [melhorias-pendentes.md](../desenvolvimento/melhorias-pendentes.md)). Agente novo não deve usá-los.

---

## 6. Atualização OTA (só Android)

1. O ADM publica o APK na página **Agente**. O servidor exige a senha do ADM, arquivo `.apk`
   válido de até 100 MB e `versionCode` **maior** que o vigente. O arquivo fica em
   `Storage:PacotesAgentePath`, com hash SHA-256 em hex maiúsculo.
2. O heartbeat oferece o pacote a quem envia um `agenteVersaoCode` menor.
3. O agente ignora a oferta se `versaoCode <= BuildConfig.VERSION_CODE`. Ele instala uma
   atualização por vez e exige Device Owner ou a permissão de instalar apps desconhecidos.
4. `GET /api/agente/atualizacao/apk` → o agente salva o arquivo e **confere o SHA-256** (sem
   diferenciar maiúsculas). Hash diferente: o arquivo é apagado e a oferta é tentada de novo no
   próximo heartbeat.
5. Instala via `PackageInstaller`. No Android 12+ sem interação, da segunda atualização em diante.

---

## 7. Localização

| Agente | Fonte | Precisão | Origem gravada |
|---|---|---|---|
| Windows | `http://ip-api.com/json/…` (IP público; ver [item 17](../desenvolvimento/melhorias-pendentes.md#17-geolocalização-do-windows-por-http-em-serviço-gratuito)) | cidade (fixa em 5000 m) | `IpGeolocationWindows` |
| Android | Fused Location do Google (última posição de até 5 min; senão, leitura nova com timeout de 20 s) | real | `GpsAndroid` (mesmo quando a posição veio da rede) |

Cada heartbeat com coordenadas **grava uma linha nova**, sem deduplicação e sem expurgo. Ver
[privacidade-lgpd.md](../privacidade/privacidade-lgpd.md) e o [item 15](../desenvolvimento/melhorias-pendentes.md#15-localização-gravada-a-cada-minuto-para-sempre).
As origens `WifiConhecida` e `ManualTI` existem no enum, mas nada as grava.

---

## 8. Configuração dos agentes

**Windows**: seção `Agente` do `appsettings.json` (o instalador do pendrive já o gera preenchido).

| Chave | Padrão | Observação |
|---|---|---|
| `BackendBaseUrl` | `http://localhost:5171` | Em produção, `https://tracker.foodsacademy.com.br` |
| `AgentToken` | `""` | Só para instalação manual com token do painel |
| `EnrollmentKey` | `""` | A chave da empresa, para o auto-cadastro |
| `Tipo` | `"Notebook"` | |
| `HeartbeatIntervalSeconds` | `60` | |
| `Simular` | `false` no JSON (**`true` no código**) | `true` = não trava a tela de verdade (desenvolvimento) |

Caminhos fixos:

- `%ProgramData%\TrackerAgent\locked.flag` (o antigo `overlay-alive.flag` foi abolido; o
  instalador o apaga);
- `token.dat` e `agent.log`, ao lado do executável;
- serviço `TrackerAgent`;
- tarefa agendada `TrackerAgentLock`.

**Android** (valores de compilação, não de runtime):

- **URL do backend:** o release usa sempre o `gradle.properties`
  (`https://tracker.foodsacademy.com.br`) e é recusado se ela for `http://`. O debug usa
  `tracker.backendBaseUrl` do `local.properties` e, **na falta dela, cai para produção**.
- Constantes em `AgenteConfig.kt`:
  - heartbeat: 60 s;
  - verificação do hub: 5 s;
  - reaplicação do bloqueio: 2 s;
  - nova tentativa após erro: 30 s.
  - O `VigilanteWorker` roda a cada 15 min.

---

## Diferenças entre os agentes (resumo)

| Recurso | Windows | Android |
|---|---|---|
| Recadastro automático no 401 | ✅ | ❌ (registro manual) |
| Bloqueio sobrevive ao 401 (token apagado) | ✅ (`locked.flag`) | ✅ (sinal `bloqueado`), mas fica preso sem `adb` |
| Reaplica bloqueio por `deveEstarBloqueado` | ✅ (ignora resposta mais velha que o último comando) | ✅ (idem) |
| Deduplica comando por `id` | ✅ | ✅ (últimos 50) |
| `tipoComando` numérico aceito | ✅ | ✅ |
| Serial quando falta o de fábrica | `WIN-<UUID>` / `WIN-<MachineGuid>` / `HOST-<nome>` | série digitada / `ANDROID-<ANDROID_ID>` |
| Alertas | só `BloqueioReaplicado` | ✅ |
| OTA | ❌ (reinstalar com o `.exe`) | ✅ |
| Versão real reportada | ✅ (tag da release; instaladores anteriores a 13/10/2026 mandam `1.0.0`) | ✅ |
| Bateria, modelo, proteção | ❌ | ✅ |
| Reconexão do hub | perpétua (`RecuoHub` + laço de `StartAsync`) | laço próprio |

---

## Escrevendo um agente novo: checklist

- [ ] Tipo novo de dispositivo? Valor novo **no fim** do enum, com migração e subclasse de
      `DispositivoComAgente`. Inclua o tipo em `AgentAuth.ResolverAsync` e em
      `BuscaController.BuscarDispositivos`.
- [ ] Guardar o token de forma protegida e tratar o 401 (recadastro ou aviso).
- [ ] Deduplicar comandos pelo `id` (o heartbeat reentrega até a confirmação) e só confirmar
      depois de executar.
- [ ] Aceitar `tipoComando` como texto e como número.
- [ ] Persistir o bloqueio localmente (sobreviver a reinício e à falta de rede) e respeitar
      `deveEstarBloqueado`.
- [ ] Ignorar o `deveEstarBloqueado` de uma resposta que saiu antes do último comando aplicado.
- [ ] Nunca amarrar a reaplicação do bloqueio à existência do token.
- [ ] Não mandar serial genérico de BIOS (ver `SerialGenerico`): sem patrimônio, o servidor recusa.
- [ ] Enviar `origem` na localização e evitar pontos repetidos.
- [ ] Reportar a versão real (`agenteVersao` e, se houver OTA, `agenteVersaoCode`).
