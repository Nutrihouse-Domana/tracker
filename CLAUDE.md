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
docs/                      Guias de implantação, dev, CI/CD e agentes (índice: COMECE-AQUI.md).
```

**Guia novo em `docs/` entra nos DOIS índices** — a tabela "Documentação" do `README.md` e a
"Guias que você tem" do `docs/COMECE-AQUI.md`. Os dois já ficaram defasados (sem Android, CI,
pendrive, ambiente-dev). Ao mudar um fluxo (ex.: instalação do agente, deploy), atualize também o
guia que o descreve: o `deploy-automatico-cicd.md` chegou a afirmar que o CI baixava .NET/Node
via `setup-*`, o que o egress impede. **Mudou o comportamento de uma tela** (botão, regra,
confirmação, permissão)? Atualize `docs/manual-do-usuario.md` — inclusive a seção "Limitações
conhecidas" quando corrigir uma delas. **Limitação conhecida vive em `docs/melhorias-pendentes.md`**
(problema, onde, correção sugerida, como verificar): ao corrigir, marque o item como resolvido
lá (data + PR) e tire-o do manual; ao descobrir uma nova, acrescente nos dois. Chave/segredo novo entra na tabela de `docs/backup-e-chaves.md`.
**Nunca escreva o VALOR de chave ou código de instalação em doc versionado** (o `agente-android.md`
já trouxe o `Enrollment:CompanyKey` real): use marcador `<CODIGO-DO-COFRE>`.

Frontend organizado por feature em `src/Tracker.Web/src/features/<feature>/` (api.ts + hooks.ts + componentes).

## Ambiente de desenvolvimento (Windows)

**Máquina nova?** `.\deploy\preparar-ambiente-dev.ps1` faz tudo: sobe o SQL Server em container
(`docker-compose.yml`, porta **11433** no host para não brigar com um SQL Server já instalado),
gera o `.env` com a senha do SA e grava os `user-secrets` que faltarem. É **idempotente** e
**preserva** o que já existe — em especial `Encryption:CpfKeyBase64`, que regenerada tornaria
ilegíveis os CPFs já gravados, e a `ConnectionStrings:TrackerDb` de quem já usa SQL Express
local (para apontar ao container mesmo assim: `-Forcar`; para pular o Docker: `-SemDocker`).
Guia: `docs/ambiente-dev.md`. **Produção não usa Docker** — lá é SQL Server on-premises com IIS.
O Docker Desktop precisa do **WSL2**; sem ele o engine nunca fica pronto e o sintoma é idêntico
ao de "Docker fechado" (`wsl --install` como Administrador + reiniciar). O script detecta e avisa.

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
# Release da frota: use o script (cria a keystore na 1a vez, compila e mostra versionCode/SHA-256).
./deploy/gerar-apk-agente.ps1
# assembleRelease direto exige keystore em local.properties — falha com mensagem se faltar.

# Instalar/diagnosticar no aparelho (adb vem do SDK)
C:\Android\Sdk\platform-tools\adb.exe install -r app/build/outputs/apk/release/app-release.apk
```

## Convenções e padrões

- **Entidades** herdam de `EntidadeBase` (Id Guid, CriadoEm). Dispositivo usa **TPH**
  (Table-per-Hierarchy): `Dispositivo` (abstrata) → `Notebook`/`Mobile` (via
  `DispositivoComAgente`) e `Periferico`. Discriminador `Tipo`.
- **`Mobile` cobre celular E tablet.** Não existe tipo separado para tablet: o agente é o mesmo e
  separá-los não mudava nada no controle de ativos. `Mobile.Imei` é nulo em aparelhos só-Wi-Fi.
- **`Dispositivo.IdentificacaoInterna` é o rótulo MANUAL da TI** (nulo por padrão, na base → todos os
  tipos). É a única identificação editável depois do cadastro: série/patrimônio/hostname só entram na
  criação ou vêm do agente. **Existe porque o hostname NÃO serve como identificação editável — o
  heartbeat o sobrescreve a cada ciclo** (`AgenteController.Heartbeat`); este campo é da TI e o agente
  nunca o toca. Quando preenchido, é a identificação EXIBIDA (`identificacaoInterna ?? hostname ??
  numeroSerie`) na lista e no título do detalhe; o hostname técnico então reaparece numa célula
  própria. Endpoint `PUT /api/dispositivos/{id}/identificacao-interna` (`DefinirIdentificacaoInterna`)
  exige `dispositivos.cadastrar` **e a senha do ADM** (400, não 401, em senha errada) e audita
  (`DispositivoIdentificacaoInternaDefinida`). Enviar vazio limpa o campo (volta ao fallback).
- **Alugado x próprio é CONVENÇÃO DE NOME, não campo do cadastro.** A identificação EXIBIDA
  começando com `MAQ` (sem diferenciar maiúsculas) marca equipamento alugado — regra centralizada em
  `src/Tracker.Web/src/features/dispositivos/aluguel.ts` (`ehAlugado`, `rotuloAluguel`,
  `identificacaoExibida`), usada na coluna "Aluguel" de `DispositivosPage`. Use `identificacaoExibida`
  em vez de repetir `identificacaoInterna ?? hostname ?? numeroSerie`. **A regra tem um espelho no
  backend** — `src/Tracker.Application/Relatorios/ClassificacaoAluguel.cs`, usado pelos
  relatórios (que leem o banco, não a lista carregada na tela): mudar o critério exige mexer nos
  DOIS lados, senão a coluna e o relatório discordam sobre o mesmo aparelho. Consequência a lembrar: como o
  fallback inclui o **hostname**, e o heartbeat sobrescreve o hostname a cada ciclo, um aparelho SEM
  `IdentificacaoInterna` pode mudar de classificação sozinho — só o rótulo manual da TI é estável.
  Se um dia a origem virar dado de verdade, isso vira campo no `Dispositivo` e a função sai daqui.
- **`Dispositivo.VencimentoAluguel` (DateOnly?) é o fim do CONTRATO DE ALUGUEL**, não garantia nem
  vida útil. Nulo = sem contrato (o caso dos próprios); o campo existe em todos os tipos, mas quem
  diz que o ativo é alugado continua sendo o prefixo `MAQ` da identificação. Endpoint
  `PUT /api/dispositivos/{id}/vencimento-aluguel` exige `dispositivos.cadastrar` e **NÃO pede a
  senha do ADM** (é dado operacional do contrato, não mudança de responsabilidade) — mas audita
  (`DispositivoVencimentoAluguelDefinido`). No site, a regra de situação
  (vencido / vence em ≤30 dias / em dia) fica em `features/dispositivos/vencimento.ts` e alimenta a
  coluna "Vence em", o chip do detalhe e o alerta do topo da lista — que conta sobre a lista inteira,
  não a página visível, senão o aviso sumiria ao paginar.
- **DATA CIVIL É `DateOnly`, NUNCA `DateTime` (pegadinha que já foi a produção).** A API registra o
  `UtcDateTimeConverter` (`src/Tracker.Api/Json/`), que carimba **todo** `DateTime` com `Z` — certo
  para timestamps (gravados em UTC e devolvidos pelo SQL Server com `Kind=Unspecified`), mas fatal
  para uma data sem hora: `2026-10-05` saía `2026-10-05T00:00:00.000Z` e o navegador em UTC-3
  exibia **04/10**. `DateOnly` não passa pelo conversor, vira coluna `date` no banco e trafega como
  `"2026-10-05"`. **No front, `new Date("2026-10-05")` erra igual** — o JS lê string só-data como
  UTC e volta um dia; faça parse manual (`vencimento.ts` tem o helper). Coberto por
  `VencimentoAluguelTests`, que serializa com as MESMAS opções do `Program.cs`.
- **Relatórios do inventário** (`RelatoriosController`, `GET /api/relatorios/{tipo}/{pdf|xlsx}` com
  tipo = `inventario`/`aluguel`/`unidades`): dados em `Tracker.Infrastructure/Relatorios`
  (`RelatorioInventarioService` monta as linhas; `RelatorioPdfService` e `RelatorioExcelService`
  renderizam). São LEITURA do parque ativo → pedem só `dispositivos.ver` e **não** registram
  auditoria (que é para escrita). No site ficam no **menu da conta** (`MenuRelatorios`, aberto ao
  clicar no próprio nome), não na página de Perfil — esta só mostra quem está logado e suas
  permissões. A coluna de data dos relatórios é **`CriadoEm` ("Cadastrado em")**, não
  `DataAquisicao`: o formulário de cadastro grava `DataAquisicao = agora` e não há campo na tela
  para outra data, então ela nunca foi data de compra de verdade. `CriadoEm` é UTC e o SQL Server
  o devolve com `Kind=Unspecified` — marque como UTC antes de `ToLocalTime()`, senão cadastro da
  noite sai com a data do dia seguinte.
- **XLSX é gerado com ClosedXML** (0.102.3, em `Tracker.Infrastructure`). `AdjustToContents` NÃO
  aceita `maxWidth:` nesta versão (erro de compilação) — ajuste e depois limite `coluna.Width` à
  mão. Escreva data/número com o TIPO nativo da célula: como texto, o filtro e a ordenação do
  Excel tratam "10/01/2025" como string.
- **`GlobalFontSettings.FontResolver` do PdfSharpCore é global e só pode ser atribuído uma vez.**
  Todo gerador de PDF chama `PdfFontes.Garantir()` (`Tracker.Infrastructure/Pdf/PdfFontes.cs`), que
  registra o `TermoFontResolver` sob lock. Não escreva em `GlobalFontSettings` direto num serviço novo.
- **DTOs** são `record` na camada Application. Enums serializados como string na API.
- **Validação** com FluentValidation, injetada nos controllers via `IValidator<T>`.
- **CPF** é criptografado em repouso (AES-GCM) via `EncryptedStringConverter` no DbContext.
- **Ações sensíveis pedem a senha do ADM logado** (`ConfirmacaoSenhaAdm` em `src/Tracker.Api/Seguranca`):
  excluir dispositivo, alternar `PertenceAUsuario` e definir `IdentificacaoInterna`. Cookie de sessão
  não prova quem está na máquina.
- **Autenticação:** ADM via ASP.NET Core Identity (cookie). **Agente** via token por dispositivo
  no header `X-Agent-Token` (hash SHA-256 guardado em `TokenAgenteHash`), resolvido por `AgentAuth`.
- **Permissões (autorização):** cada capacidade é uma **permissão granular** gravada como claim
  `perm` no `UsuarioADM` (tabela AspNetUserClaims) e vira uma **policy homônima** em `Program.cs`.
  Catálogo central em `src/Tracker.Application/Seguranca/Permissoes.cs` (constantes + rótulos/grupos
  para a UI) — **novo valor entra aqui e em `Todas`; nunca renomeie um valor já em uso**. Controllers
  usam `[Authorize(Policy = Permissoes.X)]`. GETs que alimentam formulários (unidades, detentores)
  pedem `dispositivos.ver`, não a permissão de gestão. `acessos.gerir` (gerir LOGINS) ≠
  `usuarios.gerir` (gerir DETENTORES); a tela nova de logins chama-se **"Acessos"**, a antiga de
  detentores continua **"Usuários"**.
- **Auditoria:** toda ação de escrita de ADM chama `IAuditoria.Registrar(...)`
  (`src/Tracker.Api/Seguranca/Auditoria.cs`), que grava um `LogAuditoria` (quem/quando/o quê) na
  mesma transação. O autor vem SEMPRE do usuário logado (`User.AdmId()`/`User.AdmNome()`), nunca do
  corpo. Leitura em `AuditoriaController` (permissão `auditoria.ver`).
- **Comandos remotos:** ADM emite via REST; entrega ao agente por **SignalR** (`/hubs/comandos`)
  com fallback pelo **heartbeat** (o agente também busca comandos pendentes a cada heartbeat).
- **Atualização OTA do agente Android (sem loja):** a TI publica o APK no painel (página Agente,
  com senha do ADM — `AgenteAndroidController`); o heartbeat compara `AgenteVersaoCode` com o
  maior `PacoteAgenteAndroid.VersaoCode` e anuncia a oferta; o agente baixa
  (`GET /api/agente/atualizacao/apk`, token do dispositivo), confere o SHA-256 e se instala via
  `PackageInstaller`. APKs ficam em `App_Data/pacotes-agente` (`Storage:PacotesAgentePath`).
  **Em produção `Storage__PacotesAgentePath` PRECISA apontar para fora de `C:\Tracker`** (ex.:
  `C:\TrackerDados\pacotes-agente`): o padrão é relativo ao ContentRoot, e o
  `atualizar-servidor.ps1` apaga a pasta do site inteira a cada deploy — o banco seguiria
  anunciando o APK, mas o arquivo teria sumido e a OTA falharia até republicar.

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
- **`StatusDispositivo.EmUso` NÃO é transição manual livre.** Ele entra automaticamente ao atribuir
  detentor (`AtribuirUsuario`) ou concluir reparo. O menu manual (`StatusDispositivoAcoes.tsx`) só
  oferece estoque/manutenção/baixa — e mais **"Colocar em uso" apenas para ativo de local**
  (`PertenceAUsuario = false`) **com unidade definida** (o de detentor já vira "Em uso" na
  atribuição). O backend (`AtualizarStatus`, `DispositivosController.cs`) reforça a mesma regra:
  recusa `EmUso` se for de usuário sem detentor OU de local sem unidade. Gate os DOIS lados.
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
- **Token inválido re-cadastra sozinho (401).** Antes, o agente usava o `token.dat` cegamente e,
  se o backend rejeitasse (agente atualizado ou apontado para outro servidor), repetia **401** para
  sempre e o aparelho nunca aparecia no inventário. Agora `BackendClient` lança `TokenInvalidoException`
  no 401, o `Worker` chama `EnrollmentService.ReenrollarAsync` (apaga `token.dat` → auto-cadastro) e
  reconecta o hub com o token novo. Não duplica: o `AutoEnrollment` reencontra o dispositivo por
  patrimônio→série→IMEI e só regenera o token. Diagnóstico manual continua valendo: apagar
  `C:\TrackerAgent\token.dat` e reiniciar o serviço força o re-cadastro na hora.
- **Wi-Fi na tela de bloqueio: NÃO liste redes com `netsh wlan show networks`.** O netsh só devolve
  o **cache** da última varredura e **nunca dispara uma nova** — numa máquina ociosa/desconectada
  (o caso exato da trava) ele retorna só a rede conectada, ou nada, e o usuário fica sem opção para
  reconectar e receber o "Desbloquear". Pior: builds do Windows sem `netsh wlan scan` não têm como
  forçar a busca por linha de comando. `WifiConnect.ListarRedes` usa a **Native Wifi API**
  (`wlanapi.dll`): `WlanScan` (varredura real) → espera a notificação ACM `scan_complete` (até 5s) →
  `WlanGetAvailableNetworkList`. Medido na prática: netsh via 1 rede, a API nativa 5. O netsh fica
  só como fallback e para **conectar** (que funciona uma vez que se tem o SSID). O scan bloqueia
  ~5s, então `AtualizarRedes` (em `LockOverlay`) roda em `Task.Run` e devolve por `BeginInvoke` —
  senão a tela de bloqueio congela durante a varredura. O delegate de notificação precisa ficar
  vivo (`GC.KeepAlive`) durante a espera.
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
  - **A keystore de release amarra a frota inteira**: assinatura diferente é recusada pelo Android
    e a saída seria reinstalar aparelho por aparelho. Ela mora **fora do repositório**
    (`Documents\TrackerKeystore\`) porque worktree/clone novo não leva a chave junto.
    **O release da frota sai do CI** (`.github/workflows/apk-agente-android.yml`, disparo manual):
    a chave vive como secret (`ANDROID_KEYSTORE_BASE64` + as 3 senhas), é decodificada num arquivo
    **fora do workspace**, usada e apagada num `finally` — assim qualquer pessoa gera o APK sem ter
    a chave na máquina, que é o que evita espalhá-la. As senhas vão por `ORG_GRADLE_PROJECT_*`, não
    por `-P`: argumento de linha de comando aparece no `tasklist` do servidor. **No runner não há
    `local.properties`**, então o SDK é achado por `ANDROID_HOME` — e o runner precisa de JDK 17 e
    Android SDK pré-instalados (o egress bloqueia os CDNs, igual ao deploy). O build de debug NÃO
    está no workflow de propósito: sem `local.properties` ele sairia apontando para produção.
  - **O release liga o R8.** `proguard-rules.pro` preserva `dados.**` (o Gson do SignalR lê os
    campos por nome) e o Device Admin; receivers do manifest o AGP mantém sozinho. DTO novo fora
    de `dados` exige regra nova — sem ela o comando chega vazio e é descartado **em silêncio**.
- **Identificação Android — o `NumeroSerie` provisório NÃO é estável.** Sem Device Owner o agente
  manda `ANDROID-<ANDROID_ID>`, e o ANDROID_ID é escopado por **chave de assinatura do APK**: ele
  muda quando o app é reinstalado assinado com outra chave (trocar debug→release faz isso na frota
  inteira). Já duplicou tablet em produção assim. Consequências:
  - O `AutoEnrollment` reencontra o ativo nesta ordem: **patrimônio → número de série → IMEI**.
    O patrimônio é digitado pelo técnico e é o único que atravessa troca de assinatura.
  - `PromoverNumeroSerieAsync`/`PromoverPatrimonioAsync` só **sobem** a qualidade do dado: serial
    de fábrica nunca é rebaixado para `ANDROID-*`, patrimônio real nunca volta a `AUTO-*`. As duas
    checam o índice único **com `IgnoreQueryFilters()`** (arquivado seguraria o UPDATE).
  - Arquivado com o mesmo serial **ou patrimônio** devolve 409 com mensagem clara.
  - O heartbeat completa o `Imei` só quando o cadastro está vazio — nunca sobrescreve o digitado.
- **Ler série/IMEI de fábrica depende da versão do Android:** até o 7.1 `Build.SERIAL` é livre; do
  8.0 ao 9 exige `READ_PHONE_STATE` (declarada com `maxSdkVersion=28`, pedida no enrollment); do
  **10 em diante é proibido a app comum — só Device Owner**, e nenhuma permissão contorna. Tablet
  sem modem (`FEATURE_TELEPHONY` ausente) não tem IMEI algum: o campo some da tela em vez de
  convidar o técnico a inventar um número.
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
  - **A identidade do aparelho é o número de série DIGITADO pelo técnico no registro**, não um
    identificador do sistema. O Android não entrega série de fábrica nem IMEI a um app comum
    (bloqueio desde a versão 10), e o `ANDROID_ID` que sobra **muda quando a assinatura do APK
    muda** — trocar o keystore duplicou todos os registros do inventário. A série da etiqueta é
    estável e sobrevive a troca de chave, reinstalação e limpeza de dados.
  - **`dpm remove-active-admin` só funciona com app instalado em modo de teste.** Em produção o
    Android recusa (`Attempt to remove non-test admin`), e como o admin ativo bloqueia a
    desinstalação, nem `adb uninstall` passa. O caminho é desativar à mão em
    Configurações > Segurança > Apps de administração do dispositivo. Abra direto com
    `adb shell am start -n 'com.android.settings/.Settings$DeviceAdminSettingsActivity'`.
- **Keystore do APK: guarde FORA do repositório e com backup externo.** Um keystore dentro da
  árvore de trabalho já foi perdido (a pasta era descartável e sumiu). Sem ele não há como
  atualizar o app nos aparelhos instalados — só desinstalando um a um, e cada reinstalação com
  assinatura nova duplica o registro no inventário. Hoje fica em `C:\NutrihouseChaves`, apontado
  por caminho absoluto em `local.properties`.
- **A URL do backend no APK é amarrada ao buildType**, não ao `local.properties`: `release` usa
  sempre `gradle.properties` (produção) e `debug` usa o `local.properties` da máquina. Sem isso,
  um pacote gerado na máquina de quem estava testando sairia apontando para o laboratório — falha
  silenciosa que só apareceria depois de instalada na frota.
  **O outro lado da moeda:** sem `tracker.backendBaseUrl` no `local.properties`, o build **debug**
  cai para a URL de PRODUÇÃO. E o Android Studio cria o `local.properties` sozinho **só com
  `sdk.dir`** ao abrir o projeto — então alguém que clone, compile o debug e instale num tablet de
  teste **auto-cadastra o aparelho no inventário real**. Verificado na prática: sem a chave o
  `BuildConfig.BACKEND_BASE_URL` sai `https://tracker.foodsacademy.com.br`. Por isso
  `deploy/preparar-ambiente-dev.ps1` grava a chave apontando para o IP desta máquina
  (`http://<ip>:5171`) — e o `sdk.dir` precisa da barra **escapada** (`C:\Android\Sdk`), porque no
  formato `.properties` do Java a barra é escape e o Gradle leria `C:AndroidSdk`.
- **Scripts PowerShell (.ps1):** escreva em **ASCII puro** (sem acentos/travessão) — o Windows
  PowerShell 5.1 lê `.ps1` sem BOM na codepage do sistema e quebra com caracteres especiais.
- **PowerShell + `.exe`: com `$ErrorActionPreference = "Stop"`, o STDERR de um comando externo vira
  erro TERMINANTE quando a saída do script está redirecionada** (`.\script.ps1 > log.txt 2>&1`, ou o
  script chamado de dentro de outro que captura a saída). E `docker`, `dotnet` e `npm` escrevem
  **progresso** no stderr, não só erros — então o script morre no meio de uma execução perfeitamente
  normal, **antes** de chegar ao `if ($LASTEXITCODE -ne 0)`, e a mensagem de erro amigável que você
  escreveu nunca aparece (foi assim que a dica sobre o WSL ausente ficava escondida atrás de um
  `NativeCommandError` cru). `preparar-ambiente-dev.ps1` resolve com os helpers `ExecutarExterno` /
  `ExecutarExternoComSaida` / `ExecutarExternoVisivel`, que baixam o preference para `Continue` só em
  volta da chamada nativa e devolvem o `$LASTEXITCODE` para a checagem. Use o mesmo padrão em script
  novo que chame `.exe`. **De brinde:** `docker compose up -d` sem `--quiet-pull` despeja milhares de
  linhas de progresso do download e esconde as mensagens do próprio script.
- **MigraDoc:** o pacote é `MigraDocCore.*` (namespaces `MigraDocCore.DocumentObjectModel`/`.Rendering`).
- **Termo de uso (PDF):** `TermoDeUsoPdfService` replica FIELMENTE o modelo oficial em Word (`NOVO
  MODELO DE TERMO DE RESPONSABILIDADE.doc`): página **Carta** (não A4), Calibri Light 10,5pt,
  entrelinha simples, espaçamento por linhas em branco, rótulos sem negrito, sem rodapé de versão.
  O texto legal segue o modelo oficial, com 3 correções de digitação do original já aplicadas
  (duplicação "com endereço", vírgula após o CEP, espaço antes de "6."/"7."). Não alterar o texto
  sem validar com RH/jurídico (é o texto assinado). Versão do termo bumpada para **2.0** no front
  (`TermoDeUsoSection.tsx`); termos antigos ficam v1.0.
  **PEGADINHA de produção (RESOLVIDA):** o PdfSharpCore resolve a fonte pelas fontes do SISTEMA e,
  sem resolver, ESTOURA em runtime se a Calibri faltar. Há um `TermoFontResolver` (no mesmo arquivo,
  registrado 1x em `GlobalFontSettings.FontResolver`) que lê os `.ttf` do `%WINDIR%\Fonts`: prefere
  Calibri Light (`calibril.ttf`/`calibrib.ttf`) e cai para **Arial** (sempre presente no Windows
  Server) — nunca versione fonte no repositório. Só funciona em Windows (dev e prod são Windows).
- **MUI v9:** use `sx={{ justifyContent, alignItems }}` no `Stack` (não como props soltas);
  `slotProps={{ input: {...} }}` em vez de `InputProps`. Ícones existem só na variante `*Outlined` etc.
- **Tema claro/escuro (dark mode):** `theme.ts` NÃO exporta um tema fixo pronto — exporta
  `criarTema(modo: 'light' | 'dark')` (factory) e um `theme` claro só para fora do provider (testes).
  O runtime usa `ModoTemaProvider` (`src/Tracker.Web/src/features/tema/`), que segura o modo, monta
  `ThemeProvider` + `CssBaseline`, persiste a escolha em `localStorage['tracker.tema']` e, sem escolha,
  segue `prefers-color-scheme` do SO. Ele também escreve `document.documentElement.style.colorScheme`
  (senão inputs nativos e scrollbar ficam fora de sintonia). Alterna via `useModoTema().alternar`
  (botão sol/lua no `AppShell`). **As telas quase todas já usam tokens** (`text.primary`,
  `background.default`/`paper`, `divider`, `action.hover`, `Paper`) e viram no escuro sozinhas — ao
  criar tela nova, use tokens, NÃO fixe `#fff`/hex de fundo/texto. Exceções que fixam cor de
  propósito: os cards-gradiente coloridos do Dashboard (texto branco sobre cor de status, ok nos dois
  modos) e a `AppBar`, cujo override de cor é resolvido por modo dentro de `criarTema`. Dois pontos
  que precisaram de tratamento por modo (não são token e não invertem sozinhos): a **pílula da barra
  de busca** (`BarraDeBusca.tsx` — `grey.100` é cinza-claro FIXO e ficava branca sobre a AppBar
  escura; agora `theme.palette.mode === 'dark' ? 'rgba(255,255,255,0.08)' : 'grey.100'`) e o **mapa
  Leaflet** (`LocalizacaoSection.tsx` — o mapa fica SEMPRE claro, com tiles do OpenStreetMap,
  inclusive no modo escuro do site. **NÃO use `basemaps.cartocdn.com/dark_all`: a CARTO passou a
  exigir chave de API e carimba "API KEY REQUIRED" sobre o mapa** — foi o motivo de largar o dark
  tile). `cinza.100`/`grey.*` e `common.white` são FIXOS: não
  os use como fundo de algo que precise virar no escuro. **Pegadinha do `Paper` com `elevation` no
  escuro:** o MUI clareia a superfície com um **degradê de elevação** (`backgroundImage`), e se
  houver dentro um `ListSubheader` (pinta `background.paper` sólido) o painel fica com DOIS tons
  brigando — visível no dropdown da busca. Conserto: `sx={{ backgroundImage: 'none' }}` no `Paper`
  (some o degradê → cor só, como no claro) + `border: 1px solid divider` p/ separar do fundo. Vale
  para qualquer menu/popover elevado com subheaders no escuro. **Chips coloridos ficam pastel no
  escuro** (o MUI clareia as cores semânticas): escurecidos SÓ no escuro por override em `criarTema`
  (`MuiChip.styleOverrides.root`) — success `#2E7D32`, primary `#8C4142`, error `#B23B3B`, texto
  branco. **Botões `contained`** têm o mesmo problema (usam o primary clareado) e são escurecidos
  igual no `MuiButton.styleOverrides.root` (primary `#8C4142`, error `#B23B3B`, success `#2E7D32`,
  texto branco + `:hover` mais escuro) — mas o primary claro segue valendo para texto/realce fora
  de botão (nav, links). **PEGADINHA:** o slot combinado `filledSuccess`/`containedPrimary` NÃO
  existe nesta versão do MUI (erro de tipo); e as classes combinadas `.MuiChip-filledSuccess`/
  `.MuiButton-containedPrimary` também não — o runtime usa DUAS classes separadas. Selecione dentro
  do slot `root` com `'&.MuiChip-filled.MuiChip-colorSuccess'` /
  `'&.MuiButton-contained.MuiButton-colorPrimary'`.
- **Ordenação de tabelas no cliente:** hook único `useOrdenacao(itens, colunaInicial, direcao?)`
  em `src/Tracker.Web/src/utils/ordenacao.ts` (estado coluna+direção, alterna asc/desc no clique,
  comparador estável que joga `null`/`undefined` para o fim e compara texto com `localeCompare('pt-BR',
  { numeric: true })`). Cabeçalho usa `TableSortLabel` (`active`/`direction`/`onClick`). Ordena
  SEMPRE sobre a lista já filtrada (aplique busca/filtro antes de passar ao hook). O genérico é só
  `<T>` com `keyof T` — NÃO reintroduza um segundo parâmetro de tipo `K extends keyof T`: o TS o
  infere no literal da coluna inicial e recusa as demais colunas. Usado em `UsuariosPage`/`UnidadesPage`.
- **Paginação de tabelas no cliente:** hook `usePaginacao(itens, tamanhoInicial?)` em
  `src/Tracker.Web/src/utils/paginacao.ts` (padrão **10** por página, opções 10/25/50/100). Devolve
  `itensPagina` (fatia da página — mapeie ESTA no `TableBody`), `total` (tamanho da lista completa,
  usado na contagem do título "Dispositivos ({total})") e `paginacaoProps` prontos para
  `<TablePagination {...paginacaoProps} />` (renderize DENTRO do `TableContainer`, depois do `Table`,
  gate com `total > 0`). Recebe a lista **já filtrada E ordenada** (aplique busca/`useOrdenacao`
  antes). Já recua sozinho se a página atual ficar além do fim quando a lista encolhe; mas troca de
  aba/filtro que muda a lista SEM encolher (ex.: `DispositivosPage`) precisa de um `useEffect` extra
  chamando `setPagina(0)`. Usado em Dispositivos/Usuários/Unidades/Acessos. A `AuditoriaPage` NÃO usa
  este hook — ela pagina no SERVIDOR (`useAuditoria` com `pagina`/`tamanho`, `count={data.total}`);
  só o padrão foi alinhado para 10.
  **Também serve fora de tabela:** o diálogo de atribuição (`AtribuirDialog.tsx`) troca o `select` de
  usuários por busca (nome/e-mail, sem acento) + `List` paginada de **5** com as setas do
  `TablePagination`; passe `rowsPerPageOptions={[]}` **depois** do spread para esconder o seletor
  "Por página" (não cabe no diálogo). O rótulo "Selecionado: X" lê a lista COMPLETA, não a filtrada —
  senão o usuário escolhido some da tela ao digitar outra busca (o id continua submetido). A escolha
  é **única com cara de checkbox** (clicar no marcado desmarca; marcar outro troca) — `Checkbox`
  `color="default"`, e o `Mui-selected` do `ListItemButton` é sobrescrito para `action.selected`
  porque o realce padrão puxa o primary (vermelho) e o ADM pediu a lista toda em cinza. No escuro o
  `background.paper` do tema é quase preto (#1E1E1E) e o diálogo ficava um bloco preto só: aqui ele
  fixa cinza por modo (diálogo #2E2E2E / lista #3A3A3A no escuro) via `slotProps.paper` do `Dialog`
  — exceção consciente à regra de só usar tokens, restrita a este diálogo. `Checkbox`
  no MUI v9 não aceita `inputProps`: use `slotProps={{ input: { 'aria-label': ... } }}`.
- **Pacotes .NET:** fixe versões **8.x** (o feed resolve 10.x/11.x por padrão, incompatível com net8.0).

- **Permissões e auditoria (não repita):**
  - **As permissões são recarregadas a cada requisição** por um `IClaimsTransformation`
    (`PermissoesClaimsTransformation`), lendo do banco — então alterar a permissão de alguém vale já
    na próxima requisição dele, SEM novo login. **PEGADINHA GRAVE (já corrigida):** o factory padrão
    do Identity grava as user-claims (inclusive as permissões) DENTRO do cookie no login. Se a
    transformação apenas ADICIONASSE as permissões frescas, as antigas do cookie sobreviveriam e
    revogar uma permissão só valeria após relogar (falha de segurança — um usuário zerado continuava
    agindo). Por isso a transformação **remove toda claim `perm` existente e reescreve a partir do
    banco**. Ao mexer nela, mantenha esse "strip-then-reload". **Desativar** um login é diferente:
    além do flag `Ativo` (login recusa), chame `UpdateSecurityStampAsync` — o `SecurityStampValidator`
    (intervalo de 1 min em `Program.cs`) derruba a sessão aberta.
  - **`AddColumn<bool>` do `Ativo` veio `defaultValue:false`** e foi corrigido À MÃO para `true` na
    migração `AcessosEAuditoria` — senão todo login existente nasceria desativado (trancaria a TI).
    Mesma pegadinha do `PertenceAUsuario`.
  - **Upgrade de instalação antiga:** `DbSeeder.SeedAsync` faz backfill idempotente concedendo
    TODAS as permissões a quem está na role `Admin` a cada start. Sem isso, a produção (1 ADM, zero
    claims) ficaria trancada fora de tudo assim que as policies entrassem em vigor.
  - **Autoria vem do token, nunca do corpo.** Movimentação/reparo/comentário/termo antes gravavam
    `RegistradoPorAdmId`/`CriadoPorAdmId` vindos do request (forjável com vários logins) — agora
    usam `User.AdmId()`. Os DTOs de request perderam esses campos; não os traga de volta.
  - **`LogAuditoria.EntidadeId` é Guid SOLTO, sem FK** (coberto por teste): é o que faz o registro
    "excluiu o dispositivo X" sobreviver à exclusão definitiva do alvo. Na exclusão definitiva,
    `Registrar(...)` é chamado ANTES do `Remove` (mesma transação).
  - **Nada de `Task.WhenAll` sobre `UserManager`/`DbContext`.** Listar acessos com
    `Task.WhenAll(users.Select(GetClaimsAsync))` estoura em runtime ("A second operation was started
    on this context instance") — o DbContext (scoped) não é thread-safe. `AcessosController.Listar`
    carrega as permissões de todos numa consulta só (`_db.UserClaims` agrupado), sem N+1 concorrente.
  - **Anti-lockout em Acessos (dois níveis):** `AcessosController` barra (1) **auto-lockout** —
    remover a PRÓPRIA permissão `acessos.gerir` ou desativar o próprio login (comparando com
    `User.AdmId()`); e (2) **lockout global** — remover a última permissão `acessos.gerir` ativa /
    desativar o último gestor. O front reforça (esconde "desativar" para si, trava o checkbox da
    própria `acessos.gerir`), mas a regra real é no backend. Ações sensíveis (criar/editar/ativar/
    redefinir senha) pedem a senha do ADM logado (`ConfirmacaoSenhaAdm`) — devolve 400, não 401
    (senão o axios desloga).
  - **Frontend: ocultar, não desabilitar.** `usePermissao`/`RequirePermissao`/`PermissaoRoute`
    (`features/auth`) escondem menu, rotas e botões sem permissão; o que o usuário não pode usar não
    aparece, e o acesso direto por URL redireciona. Mas isso é só UX — **a autorização real é a
    policy no backend** (403). Ao criar uma capacidade nova, gate os DOIS lados.

## Produção / deploy

- Hospedagem **on-premises** (Windows + IIS) co-hospedada com o site FOOD-ACADEMY em `192.168.0.254`,
  publicado em `https://tracker.foodsacademy.com.br` reaproveitando a porta 443 via SNI.
- Em produção, o app ASP.NET Core serve o **site (wwwroot) + API** no mesmo endereço; segredos vêm
  de **variáveis de ambiente** (`ConnectionStrings__TrackerDb`, `Encryption__CpfKeyBase64`,
  `Enrollment__CompanyKey`, `Seed__*`).
- Gerar pacote: `deploy/publicar-servidor.ps1`. Guia completo: `docs/deploy-servidor-onpremises.md`.
- **CI (`.github/workflows/deploy-producao.yml`)** roda num **runner self-hosted no próprio servidor**,
  dispara em tag `v*` (build → testes → publica site+API no IIS). O último passo **regera o
  instalador do agente Windows SÓ quando o agente mudou nesta release** (diff contra a tag anterior
  em `agents/Tracker.Agent.Windows`, `deploy/gerar-pendrive.ps1`, `deploy/Instalador`). Roda
  **depois** do deploy (falha aqui não desfaz o site). Saída em `C:\TrackerAgentDist`
  (`Instalar-Agente.exe` + cópia versionada pela tag) — **fora do `wwwroot`, pois o `.exe` embute a
  EnrollmentKey**. Depende de `Enrollment__CompanyKey` estar na env da máquina do runner (mesma da
  API); `TRACKER_BACKEND_URL` e `TRACKER_AGENTE_DIST` são env opcionais (default: URL de produção e
  `C:\TrackerAgentDist`). O checkout usa `fetch-depth: 0` para o diff enxergar a tag anterior.
  (Auto-update OTA da frota Windows ainda NÃO existe — a TI baixa o `.exe` e reinstala.)
- **`node_modules` vem de um cache LOCAL do servidor, não do `npm install`.** O `actions/checkout`
  roda `git clean -ffdx` e apaga `node_modules` a cada run, então o guard
  `if (-not (Test-Path node_modules))` do `publicar-servidor.ps1` NUNCA pegava: eram **9min15s de
  `npm install` em todo deploy — 76% dos 12 min**. O passo "Restaurar node_modules" restaura de
  `C:\TrackerCI` (fora do workspace, sobrevive ao clean), chaveado pelo **hash do
  `package-lock.json`**: lock igual restaura por `robocopy` (~30s); lock alterado não acha a pasta
  daquele hash, roda `npm ci` e semeia o cache novo — impossível reaproveitar cache desatualizado.
  Guarda os 3 mais recentes. **Não use `actions/cache`**: ele trafega pelo serviço do GitHub e o
  egress do runner é restrito (mesmo motivo de não usarmos `setup-node`/`setup-dotnet`).
  Três detalhes que quebram se mexerem neles:
  - **`robocopy` devolve 1 em caso de SUCESSO** (1 = arquivos copiados; erro é só `>= 8`). O wrapper
    do `shell: powershell` do Actions lê o `$LASTEXITCODE` final e **falharia o passo** — por isso o
    step termina com `exit 0` explícito.
  - O marcador `.ok` só é gravado **depois** do robocopy. Cache interrompido no meio fica sem
    marcador e é reinstalado, em vez de restaurado pela metade.
  - Restaurar só LÊ o cache, logo a data da pasta não muda sozinha: o step faz
    `(Get-Item $cache).LastWriteTime = Get-Date`, senão a poda "3 mais recentes" apagaria justamente
    o cache em uso (e o próximo deploy pagaria os 9 min de novo).
  O passo escreve no `GITHUB_STEP_SUMMARY` se reaproveitou o cache ou reinstalou **e por quê**
  (lock alterado x primeira execução), com o tempo gasto: aparece na página do run, sem abrir o log.
- **`publicar-servidor.ps1 -SemBuild`** publica com `--no-build --no-restore`, reaproveitando o
  `dotnet build -c Release` do passo anterior do CI (em dev, sem build prévio, o publish falha — por
  isso é opcional). Seguro para o site: o `wwwroot` é lido do disco **no momento do publish** (passo
  2 do script já copiou o `dist` lá), e o publish ainda remove da pasta de saída os arquivos do site
  antigo que sumiram.
- **`$ErrorActionPreference = "Stop"` NÃO captura falha de executável nativo** (`npm`, `dotnet`).
  Sem checar `$LASTEXITCODE` depois de cada um, um build/publish quebrado passava batido e o deploy
  seguia publicando o pacote da release anterior — falha silenciosa. `publicar-servidor.ps1` checa.

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
