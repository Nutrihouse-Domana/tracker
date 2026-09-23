# Protocolo servidor ↔ agente

Contrato entre o backend do Tracker e os agentes (Windows e Android): como o agente se cadastra,
se autentica, reporta o estado e recebe comandos. Serve para diagnosticar a frota, para mexer num
agente sem quebrar o outro e para escrever um agente novo.

Referências principais: `src/Tracker.Api/Controllers/AgenteController.cs` (endpoints),
`src/Tracker.Application/Dtos/AgenteDtos.cs` (DTOs), `src/Tracker.Api/Agente/` (autenticação e
hub). Levantado em 23/09/2026: ao mudar o protocolo, **atualize este documento**.

---

## Visão geral

```
                 1x  POST /api/agente/auto-enrollment  (X-Enrollment-Key)  -> token
Agente  ───────────────────────────────────────────────────────────────────────────►  API
   │      a cada 60 s  POST /api/agente/heartbeat     (X-Agent-Token)    -> comandos, bloqueio, OTA
   │      tempo real   WS  /hubs/comandos?token=...   <- "ExecutarComando"
   │      ao executar  POST /api/agente/comandos/{id}/confirmar
   │      Android      POST /api/agente/alertas   ·   GET /api/agente/atualizacao/apk
```

- **Dois caminhos de entrega de comando:** SignalR (imediato) e heartbeat (rede de segurança).
  Um comando só sai da fila quando o agente o **confirma**.
- O painel nunca fala direto com o agente: ele grava o comando no banco e o servidor entrega.

---

## Formato

- JSON em **camelCase**.
- **Enums como texto** (`"Bloquear"`, `"Mobile"`), tanto nos controllers quanto no SignalR
  (`AddSignalR().AddJsonProtocol(...)` em `Program.cs`). Sem a configuração do SignalR, o hub
  mandava números e os agentes descartavam o comando em silêncio. Os dois agentes aceitam
  também o número, como defesa.
- Datas da API saem em UTC com `Z`. Os payloads do agente não trazem datas.
- Nenhum DTO do agente tem validador FluentValidation. Campos `string` não anuláveis são
  obrigatórios pela validação implícita do `[ApiController]` (resposta 400).

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
| `POST /api/agente/auto-enrollment` | `X-Enrollment-Key` | 200 `EnrollmentResponse` | 401 "Chave de instalação inválida." · 400 "Auto-cadastro só vale para notebook ou mobile." · 409 existe ativo **sem agente** (periférico) com a identificação · 409 "…está arquivado. Restaure-o no painel…" |
| `POST /api/agente/heartbeat` | `X-Agent-Token` | 200 `HeartbeatResponse` | 401 (sem corpo) |
| `POST /api/agente/comandos/{id}/confirmar` | `X-Agent-Token` | 204 | 401 · 404 (comando inexistente ou de outro aparelho) |
| `POST /api/agente/alertas` | `X-Agent-Token` | 204 | 401 |
| `GET /api/agente/atualizacao/apk` | `X-Agent-Token` | 200 APK (`application/vnd.android.package-archive`) | 401 · 404 "Nenhum pacote publicado." |
| `WS /hubs/comandos?token=` | query | conexão no grupo `device-{id}` | conexão abortada |
| `GET /health` | nenhuma | `{"status":"ok"}` | usado pelo deploy, não pelos agentes |

---

## 1. Auto-cadastro

**Request** (`AutoEnrollmentRequest`):

| Campo | Tipo | Obrigatório | Windows envia | Android envia |
|---|---|---|---|---|
| `hostname` | string | sim | `MachineName` | nome do aparelho (definido pelo usuário) |
| `numeroSerie` | string | sim | serial da BIOS (WMI), senão `HOST-<nome>` | série **digitada** pelo técnico, senão a de fábrica, senão `ANDROID-<ANDROID_ID>` |
| `sistemaOperacional` | string | sim | descrição do SO | `"Android"` |
| `versaoSO` | string? | | versão | `"Android 13 (API 33)"` |
| `macAddress` | string? | | 1ª interface ativa | `null` |
| `tipo` | enum | sim | `"Notebook"` (config `Tipo`) | `"Mobile"` |
| `imei` | string? | | `null` | só com Device Owner |
| `fabricante`, `modelo` | string? | | — | sim |
| `nivelProtecao` | enum? | | — | `Nenhuma` / `DeviceAdmin` / `DeviceOwner` |
| `assetTag` | string? | | — | o cliente aceita, mas **a tela de registro não pede** (patrimônio fica no painel) |

**O que o servidor faz:**

1. **Reencontra** um cadastro existente, nesta ordem: **patrimônio → número de série → IMEI**
   (IMEI só entre `Mobile`). Arquivados ficam de fora.
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
| `agenteVersao` | `"1.0.0"` fixo | `versionName` | Atualiza a versão exibida |
| `agenteVersaoCode` | — | `versionCode` | Atualiza a versão; **sem esse campo não há oferta OTA** |
| `latitude`, `longitude` | por IP | GPS/rede | Com os dois, **insere uma linha** em `HistoricoLocalizacoes` |
| `enderecoAproximado` | "cidade, região, país" | `null` | Gravado com o ponto |
| `precisaoMetros` | `5000` fixo | precisão real | Gravado com o ponto |
| `origem` | — | `"GpsAndroid"` (sempre) | Sem ela, o servidor assume `IpGeolocationWindows` |
| `bateriaPercentual` | — | sim | Atualiza |
| `nivelProtecao` | — | sim | Atualiza; **se caiu**, cria o alerta `ProtecaoRebaixada` |
| `imei` | — | com Device Owner | Grava **só se o cadastro estiver vazio** (nunca sobrescreve o digitado) |

O heartbeat grava `UltimoHeartbeatEm` e **não altera o status** do dispositivo.

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
- `deveEstarBloqueado`: vale `true` quando o status é `Bloqueado` e não há `Desbloquear` pendente.
  O Android usa esse campo para reaplicar o bloqueio se o sinal local sumir. **O Windows o ignora**
  ([melhorias-pendentes.md, item 22](../desenvolvimento/melhorias-pendentes.md#22-agente-windows-ignora-deve-estar-bloqueado)).
- `atualizacaoAgente`: o pacote de maior `versaoCode`, quando ele é maior que o
  `agenteVersaoCode` enviado; senão `null`. **O Windows ignora esse campo** (não tem OTA).

**401 no heartbeat:**

- **Windows:** apaga o `token.dat`, refaz o auto-cadastro com a chave da empresa e reconecta o hub.
- **Android:** apaga o token e **aguarda um novo registro manual**, porque não tem o código
  guardado ([item 23](../desenvolvimento/melhorias-pendentes.md#23-agente-android-não-se-recadastra-sozinho)).

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
  - **Windows:** `WithAutomaticReconnect()` do cliente .NET. Se a conexão falhar, o agente segue
    só com o heartbeat.
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
  - `Desbloquear` → `EmUso` se tiver detentor, senão `EmEstoque`.
- **Agente Windows:** deduplica por `id`, executa e confirma. Se falhar, esquece o `id` para
  tentar de novo no próximo ciclo.
- **Agente Android:** executa e confirma. Um tipo desconhecido **não** é confirmado.

**O bloqueio é persistente no aparelho**, independentemente do servidor:

- **Windows:** arquivo-sinal `%ProgramData%\TrackerAgent\locked.flag`. Um timer de 2 s reabre a
  tela cheia (`LockOverlay`, uma janela por monitor, lançada na sessão do usuário pela tarefa
  agendada `TrackerAgentLock`).
- **Android:** sobreposição `TYPE_APPLICATION_OVERLAY`, reaplicada a cada 2 s.

---

## 5. Alertas (só Android)

`POST /api/agente/alertas` com `{ "tipo": "...", "detalhe": "..." }`. Tipos
(`TipoAlertaDispositivo`):

| Tipo | Quem gera | Quando |
|---|---|---|
| `AdminDesativado` | Agente | O usuário abriu ou concluiu a desativação do administrador do dispositivo. O servidor também grava `NivelProtecao = Nenhuma` |
| `LocalizacaoNegada` | Agente | Permissão de localização negada ou revogada (uma vez por execução do serviço) |
| `BloqueioReaplicado` | Agente | `deveEstarBloqueado` veio `true`, mas o sinal local tinha sumido |
| `ProtecaoRebaixada` | **Servidor** | O heartbeat trouxe um `nivelProtecao` menor que o anterior |

O painel mostra os alertas em *Agente & Bloqueio*, e **Tratar** os marca como reconhecidos.

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

- `%ProgramData%\TrackerAgent\locked.flag` e `overlay-alive.flag`;
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
| Reaplica bloqueio por `deveEstarBloqueado` | ❌ (só o sinal local) | ✅ |
| Alertas | ❌ | ✅ |
| OTA | ❌ (reinstalar com o `.exe`) | ✅ |
| Versão real reportada | ❌ (`1.0.0` fixo) | ✅ |
| Bateria, modelo, proteção | ❌ | ✅ |
| Reconexão do hub | automática (cliente .NET) | laço próprio |

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
- [ ] Enviar `origem` na localização e evitar pontos repetidos.
- [ ] Reportar a versão real (`agenteVersao` e, se houver OTA, `agenteVersaoCode`).
