# CLAUDE.md

Orientações para o Claude Code trabalhar neste repositório. Leia antes de editar.

## O que é

Plataforma **Tracker** — rastreabilidade de ativos de TI da Nutrihouse (notebooks, telefones,
periféricos): inventário, histórico de movimentação/reparos, termo de uso, e **bloqueio remoto**
dos dispositivos via um **agente Windows**. Idioma do domínio/código: **português** (classes,
métodos, DTOs e mensagens em pt-BR). Responder ao usuário sempre em pt-BR.

## Arquitetura

Clean Architecture no backend (.NET 8):

```
src/
  Tracker.Domain/          Entidades + enums. Sem dependências externas.
  Tracker.Application/     DTOs (records), validadores FluentValidation, lógica de caso de uso.
  Tracker.Infrastructure/  EF Core (SQL Server), Identity, PDF, criptografia, storage, DbSeeder.
  Tracker.Api/             ASP.NET Core (controllers + SignalR). Em produção serve TAMBÉM o site.
  Tracker.Web/             React + TypeScript (Vite, MUI, TanStack Query, react-leaflet).
agents/
  Tracker.Agent.Windows/   Worker Service Windows: heartbeat, geolocalização, bloqueio remoto.
  Tracker.Agent.Android/   App Kotlin (tablets/celulares): mesmo protocolo, bloqueio via DevicePolicyManager.
tests/Tracker.Api.Tests/   xUnit + WebApplicationFactory.
deploy/                    Scripts PowerShell (publicar, instalar agente).
docs/                      Guias de implantação (COMECE-AQUI, deploy on-premises, teste agente).
```

Frontend organizado por feature em `src/Tracker.Web/src/features/<feature>/` (api.ts + hooks.ts + componentes).

## Ambiente de desenvolvimento (Windows)

- **`dotnet` não está no PATH do Bash.** Use PowerShell, ou no Bash: `export PATH="$PATH:/c/Program Files/dotnet"`.
  Em PowerShell, se `dotnet` não for reconhecido, recarregue o PATH da máquina do registro.
- **Banco:** SQL Server Express local → `Server=localhost\SQLEXPRESS;...` (já em `appsettings.json`).
- **Portas:** API `http://localhost:5171`, site (Vite dev) `http://localhost:5173`.
- **Segredos de dev** ficam em `dotnet user-secrets` do projeto `Tracker.Api` (NÃO em arquivos):
  `Encryption:CpfKeyBase64` (AES-256, 32 bytes base64), `Enrollment:CompanyKey`,
  `Seed:AdmEmail`, `Seed:AdmSenha`, `Seed:AdmNome`.
- Login ADM inicial é criado no start via `DbSeeder` a partir das chaves `Seed:*`. Ele **só cria
  quando não existe nenhum usuário** — senha perdida depois disso trancaria a TI fora do sistema.
  Para recuperar (preserva a conta e o histórico ligado a ela):

  ```
  dotnet run --no-build --project src/Tracker.Api -- --redefinir-senha-adm --senha "NOVA_SENHA"
  ```

  Use `--no-build` **com a API parada ou não**: sem ele, uma API rodando trava a DLL, o build falha
  e o `dotnet run` executa um binário velho — o erro que aparece então não tem relação com a causa.
  O utilitário não depende de `Encryption:CpfKeyBase64` nem do ambiente ser `Development`: ele
  busca a conta por projeção (sem materializar o CPF criptografado) e carrega os user-secrets
  sozinho. `--email` só é necessário quando há mais de um ADM.

## Comandos

```bash
# Build / testes (rode da raiz; pare a API se estiver rodando, senão trava o .dll)
dotnet build Tracker.sln
dotnet test Tracker.sln

# API (aplica migrações automaticamente no start)
dotnet run --project src/Tracker.Api --urls http://localhost:5171

# Site em dev
npm --prefix src/Tracker.Web run dev
# Type-check / build do site
npm --prefix src/Tracker.Web run build

# Migrações EF Core
dotnet-ef migrations add <Nome> --project src/Tracker.Infrastructure --startup-project src/Tracker.Api --output-dir Data/Migrations
dotnet-ef database update --project src/Tracker.Infrastructure --startup-project src/Tracker.Api

# Agente Windows
dotnet run --project agents/Tracker.Agent.Windows      # modo console (Simular=true não trava a tela)

# Agente Android (SDK em C:\Android\Sdk; gradlew versionado, não precisa instalar Gradle)
cd agents/Tracker.Agent.Android && ./gradlew :app:assembleDebug   # APK assinado p/ teste
# assembleRelease exige keystore em local.properties — falha com mensagem se faltar.
```

## Convenções e padrões

- **Entidades** herdam de `EntidadeBase` (Id Guid, CriadoEm). Dispositivo usa **TPH**
  (Table-per-Hierarchy): `Dispositivo` (abstrata) → `Notebook`/`Mobile` (via
  `DispositivoComAgente`) e `Periferico`. Discriminador `Tipo`.
- **`Mobile` cobre celular E tablet.** Não existe tipo separado para tablet: o agente é o mesmo e
  separá-los não mudava nada no controle de ativos. `Mobile.Imei` é nulo em aparelhos só-Wi-Fi.
- **DTOs** são `record` na camada Application. Enums serializados como string na API.
- **Validação** com FluentValidation, injetada nos controllers via `IValidator<T>`.
- **CPF** é criptografado em repouso (AES-GCM) via `EncryptedStringConverter` no DbContext.
- **Ações sensíveis pedem a senha do ADM logado** (`ConfirmacaoSenhaAdm` em `src/Tracker.Api/Seguranca`):
  excluir dispositivo e alternar `PertenceAUsuario`. Cookie de sessão não prova quem está na máquina.
- **Autenticação:** ADM via ASP.NET Core Identity (cookie). **Agente** via token por dispositivo
  no header `X-Agent-Token` (hash SHA-256 guardado em `TokenAgenteHash`), resolvido por `AgentAuth`.
- **Comandos remotos:** ADM emite via REST; entrega ao agente por **SignalR** (`/hubs/comandos`)
  com fallback pelo **heartbeat** (o agente também busca comandos pendentes a cada heartbeat).
- **Atualização OTA do agente Android (sem loja):** a TI publica o APK no painel (página Agente,
  com senha do ADM — `AgenteAndroidController`); o heartbeat compara `AgenteVersaoCode` com o
  maior `PacoteAgenteAndroid.VersaoCode` e anuncia a oferta; o agente baixa
  (`GET /api/agente/atualizacao/apk`, token do dispositivo), confere o SHA-256 e se instala via
  `PackageInstaller`. APKs ficam em `App_Data/pacotes-agente` (`Storage:PacotesAgentePath`).

## Pegadinhas já resolvidas (não repita)

- **TPH + EF Core:** `.OfType<DispositivoComAgente>()` NÃO traduz para SQL (classe intermediária
  sem discriminador). Consulte `_db.Set<Notebook>()`/`Set<Mobile>()` ou busque a base e faça cast.
  **Ao criar um novo tipo com agente, atualize os DOIS lugares que enumeram os tipos concretos:**
  `AgentAuth.ResolverAsync` (senão o agente não autentica) e `BuscaController.BuscarDispositivos`
  (senão o aparelho fica invisível na busca por hostname/IMEI).
- **Excluir dispositivo tem DOIS modos** (`ExcluirDispositivoRequest.Definitiva`), escolhidos pelo
  ADM no diálogo: arquivar (padrão) ou apagar de vez. Na definitiva, **`TermoDeUso` é o único
  dependente com FK `Restrict`** — o controller remove os termos à mão antes do `Remove`, senão o
  DELETE estoura com erro cru de FK; os demais dependentes saem por `ON DELETE CASCADE`. Os PDFs
  em disco só são apagados **depois** do `SaveChangesAsync`. Coberto por `UnidadeEPosseTests`.
- **O arquivamento é LÓGICO** (`ExcluidoEm`/`ExcluidoPorAdmId`/`MotivoExclusao`), com
  **filtro global** `HasQueryFilter(d => d.ExcluidoEm == null)`. Consequências:
  - Toda consulta já sai filtrada; para ver arquivados use `IgnoreQueryFilters()`.
  - **O índice único de `NumeroSerie`/`AssetTag` NÃO conhece o filtro.** Um `AnyAsync` comum não
    enxerga o arquivado e o cadastro estouraria com erro cru de banco — `ConferirDuplicidadeAsync`
    checa com `IgnoreQueryFilters()` e devolve mensagem dizendo para restaurar.
  - O `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` é suprimido de
    propósito no `Program.cs`: Dispositivo é o lado obrigatório de várias relações de histórico.
- **`AddColumn<bool>` gerado pelo EF vem com `defaultValue: false`** — o padrão do C# (`= true` em
  `PertenceAUsuario`) **não** vale para o backfill. Sem corrigir a migração à mão, todo dispositivo
  já cadastrado viraria "ativo do local" sem unidade e perderia o detentor na tela. Ver
  `UnidadeEPosseComExclusaoLogica`.
- **Senha errada em confirmação devolve 400, nunca 401.** O interceptor do axios
  (`src/Tracker.Web/src/api/client.ts`) trata 401 como sessão expirada e limpa o `me` — o ADM
  seria deslogado só por errar a senha no diálogo de exclusão.
- **`Unidade` ≠ `Local`.** Unidade é a lotação organizacional do ativo (matriz/filial) e alimenta a
  coluna "Responsável" quando `PertenceAUsuario = false`; `Local` é ponto geográfico de geofence.
- **Novos valores de enum vão no FIM, e valores existentes NUNCA mudam de número.** O número é
  gravado como discriminador na tabela: `Mobile` = 1 (era `Telefone`) e mexer nisso reclassificaria
  todos os dispositivos já cadastrados. Coberto por `MobileTphTests`.
- **Remover um valor de enum usado como discriminador exige migração de DADOS**, que o EF não gera
  sozinho (não é mudança de schema). Ver `UnificaTelefoneETabletEmMobile`: um `UPDATE` manual move
  os registros do valor extinto, senão o EF quebra ao materializá-los.
- **Agente como Serviço do Windows:**
  - O `ContentRootPath` precisa ser `AppContext.BaseDirectory` (senão não acha `appsettings.json`
    — como serviço o diretório atual é System32).
  - Um serviço (sessão 0) **não trava a tela do usuário** direto. O bloqueio é uma **tela cheia**
    (`LockOverlay`) lançada na sessão do usuário via **Tarefa Agendada** (`TrackerAgentLock`,
    principal INTERACTIVE S-1-5-4, `MultipleInstances=Parallel`). Ver `deploy/instalar-agente.ps1`.
  - Bloqueio é **persistente**: arquivo-sinal `%ProgramData%\TrackerAgent\locked.flag`. O agente
    re-trava sozinho (timer de 2s) mesmo offline/após reiniciar; só a TI remove o sinal (Desbloquear).
  - A tela de bloqueio é **uma janela por monitor** e se reconstrói ao trocar de monitor (hotplug).
- **SignalR serializa enums de forma DIFERENTE dos controllers.** O `JsonStringEnumConverter`
  registrado em `AddControllers().AddJsonOptions(...)` **não vale para o hub**: é preciso
  `AddSignalR().AddJsonProtocol(...)` separado. Sem isso o heartbeat entrega
  `tipoComando: "Bloquear"` e o hub entrega `tipoComando: 0` — os agentes esperam texto e
  **descartam o comando do hub em silêncio**, sem erro nenhum no log do servidor. O sintoma é
  bloqueio que demora ~1 heartbeat em vez de ser imediato, com a conexão SignalR aparentemente
  saudável. Afeta os agentes Windows e Android igualmente. Os agentes hoje aceitam os dois
  formatos como defesa, mas a configuração do servidor é o conserto de verdade.
- **OTA Android:**
  - **Todo release do agente PRECISA incrementar `versionCode`** em `app/build.gradle.kts` — o
    Android recusa instalar versão igual/menor, e o painel barra a publicação com mensagem.
    O agente compara com `BuildConfig.VERSION_CODE` (não existe mais `AgenteConfig.VERSAO_AGENTE`).
  - O heartbeat só oferece APK a quem **envia** `agenteVersaoCode` — o agente Windows não envia e
    nunca recebe oferta. Não dê valor padrão 0 ao campo no backend: nulo significa "não é Android".
  - Sem Device Owner, a 1ª OTA mostra o diálogo do sistema (exige "instalar apps desconhecidos",
    passo 5 do enrollment). Da 2ª em diante (Android 12+) é silencioso: o agente vira o
    instalador de si mesmo (`USER_ACTION_NOT_REQUIRED`).
  - Agentes 1.0.0 (sem OTA) não se atualizam sozinhos: a primeira troca é manual via adb.
- **Identificação Android:** o técnico pode digitar **número de série e IMEI** no enrollment (o
  Android não os entrega a app comum sem Device Owner). O auto-enrollment casa dispositivo
  existente por `NumeroSerie` e, se falhar, por `Imei` (religa em vez de duplicar); um arquivado
  com o mesmo serial devolve 409 com mensagem clara (o índice único não conhece o filtro global).
  O heartbeat completa o `Imei` só quando o cadastro está vazio — nunca sobrescreve o digitado.
- **Agente Android:**
  - **Janela só pode ser criada/removida na thread principal.** O laço de enforcement roda em
    `Dispatchers.IO`; `WindowManager.addView` a partir dele falha ao construir o `Handler` interno
    da View e a sobreposição **nunca sobe** — sobra o `lockNow()`, que é só a trava de tela do
    sistema (sintoma: "bloqueou só a tela"). `OverlayBloqueio` já salta para a main thread sozinho.
  - **O cliente Java do SignalR não tem `withAutomaticReconnect`** (o .NET tem). Se a conexão cai,
    ela fica morta até alguém reconectar: `AgenteService.lacoDeConexaoHub` faz esse papel, com
    recuo progressivo até 60s.
  - **Bloqueio ≠ travar a tela.** `lockNow()` só aciona a trava do sistema e o usuário entra com a
    senha dele. O que deixa o aparelho indisponível é a janela `TYPE_APPLICATION_OVERLAY`
    (permissão "Sobrepor outros apps", obrigatória) reaplicada a cada 2s. Sem Device Owner o Home
    ainda troca de app por um instante — só o modo quiosque fecha isso.
  - **Logs do app podem não aparecer no `logcat`** (visto num Lenovo). Diagnostique por
    `adb shell dumpsys window windows | Select-String nutrihouse` (janela com
    `appop=SYSTEM_ALERT_WINDOW` = bloqueio ativo) e por `/proc/net/tcp` (conexão do hub).
- **Scripts PowerShell (.ps1):** escreva em **ASCII puro** (sem acentos/travessão) — o Windows
  PowerShell 5.1 lê `.ps1` sem BOM na codepage do sistema e quebra com caracteres especiais.
- **MigraDoc:** o pacote é `MigraDocCore.*` (namespaces `MigraDocCore.DocumentObjectModel`/`.Rendering`).
- **MUI v9:** use `sx={{ justifyContent, alignItems }}` no `Stack` (não como props soltas);
  `slotProps={{ input: {...} }}` em vez de `InputProps`. Ícones existem só na variante `*Outlined` etc.
- **Pacotes .NET:** fixe versões **8.x** (o feed resolve 10.x/11.x por padrão, incompatível com net8.0).

## Produção / deploy

- Hospedagem **on-premises** (Windows + IIS) co-hospedada com o site FOOD-ACADEMY em `192.168.0.254`,
  publicado em `https://tracker.foodsacademy.com.br` reaproveitando a porta 443 via SNI.
- Em produção, o app ASP.NET Core serve o **site (wwwroot) + API** no mesmo endereço; segredos vêm
  de **variáveis de ambiente** (`ConnectionStrings__TrackerDb`, `Encryption__CpfKeyBase64`,
  `Enrollment__CompanyKey`, `Seed__*`).
- Gerar pacote: `deploy/publicar-servidor.ps1`. Guia completo: `docs/deploy-servidor-onpremises.md`.

## Nunca versionar

Segredos (tokens, chaves, `.pfx`), o site compilado (`src/Tracker.Api/wwwroot/`), uploads
(`src/Tracker.Api/App_Data/`), `token.dat`, `agent.log`, `bin/`, `obj/`, `node_modules/`, `publish/`.
Já cobertos pelo `.gitignore`. Mantenha `EnrollmentKey`/tokens **vazios** nos `appsettings.json` versionados.

## Verificação ao terminar mudanças

- Backend: `dotnet build Tracker.sln` e `dotnet test Tracker.sln`.
- Frontend: `npm --prefix src/Tracker.Web run build` (faz type-check).
- Se mexeu no agente Windows: rode em console com `Simular=true` para não travar a própria tela.
- Se mexeu no agente Android: `./gradlew :app:assembleDebug` e teste o bloqueio num aparelho de
  laboratório — **não no próprio celular**, o bloqueio é persistente e só a TI remove.
- **Kotlin + anotação em `val` só-getter:** use `@get:SuppressLint(...)`. Sem o `@get:` o
  compilador recusa ("annotation is not applicable to target 'member property without backing
  field'"). Ver `InfoAparelho.numeroSerie`.
