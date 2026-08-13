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
tests/Tracker.Api.Tests/   xUnit + WebApplicationFactory.
deploy/                    Scripts PowerShell (publicar, instalar agente).
  Instalador/              Instalador do agente para pendrive: .exe unico, autocontido.
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
- Login ADM inicial é criado no start via `DbSeeder` a partir das chaves `Seed:*`.

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
```

## Convenções e padrões

- **Entidades** herdam de `EntidadeBase` (Id Guid, CriadoEm). Dispositivo usa **TPH**
  (Table-per-Hierarchy): `Dispositivo` (abstrata) → `Notebook`/`Telefone` (via `DispositivoComAgente`)
  e `Periferico`. Discriminador `Tipo`.
- **DTOs** são `record` na camada Application. Enums serializados como string na API.
- **Validação** com FluentValidation, injetada nos controllers via `IValidator<T>`.
- **CPF** é criptografado em repouso (AES-GCM) via `EncryptedStringConverter` no DbContext.
- **Autenticação:** ADM via ASP.NET Core Identity (cookie). **Agente** via token por dispositivo
  no header `X-Agent-Token` (hash SHA-256 guardado em `TokenAgenteHash`), resolvido por `AgentAuth`.
- **Comandos remotos:** ADM emite via REST; entrega ao agente por **SignalR** (`/hubs/comandos`)
  com fallback pelo **heartbeat** (o agente também busca comandos pendentes a cada heartbeat).

## Pegadinhas já resolvidas (não repita)

- **TPH + EF Core:** `.OfType<DispositivoComAgente>()` NÃO traduz para SQL (classe intermediária
  sem discriminador). Consulte `_db.Set<Notebook>()`/`Set<Telefone>()` ou busque a base e faça cast.
- **Agente como Serviço do Windows:**
  - O `ContentRootPath` precisa ser `AppContext.BaseDirectory` (senão não acha `appsettings.json`
    — como serviço o diretório atual é System32).
  - Um serviço (sessão 0) **não trava a tela do usuário** direto. O bloqueio é uma **tela cheia**
    (`LockOverlay`) lançada na sessão do usuário via **Tarefa Agendada** (`TrackerAgentLock`,
    principal INTERACTIVE S-1-5-4, `MultipleInstances=Parallel`). Ver `deploy/instalar-agente.ps1`.
  - Bloqueio é **persistente**: arquivo-sinal `%ProgramData%\TrackerAgent\locked.flag`. O agente
    re-trava sozinho (timer de 2s) mesmo offline/após reiniciar; só a TI remove o sinal (Desbloquear).
  - A tela de bloqueio é **uma janela por monitor** e se reconstrói ao trocar de monitor (hotplug).
  - **SDDL do serviço:** conceda `CCDCLCSWRPWPDTLOCRSDRCWDWO` a SYSTEM (SY) e Administradores (BA).
    A máscara antiga (`CCLCSWRPWPDTLOCRRC`) omitia `SD`/`DC`/`WD` e produzia um serviço que **nem
    administrador removia ou reconfigurava** — reinstalar morria com `sc create` 1073. Para destravar
    uma máquina nesse estado, apague `HKLM\SYSTEM\CurrentControlSet\Services\TrackerAgent\Security`.
- **Instalação do agente por pendrive** (`deploy/Instalador`, guia em `docs/instalacao-agente-pendrive.md`):
  - `.cmd`/`.bat` **não têm manifesto e nunca conseguem pedir elevação**. Só um `.exe` declarando
    `requireAdministrator` faz o Windows exibir a caixa de usuário e senha; por isso o instalador é
    um executável .NET, não um script.
  - A **Execution Policy do PowerShell está bloqueada por GPO** nas máquinas da rede, e
    `-ExecutionPolicy Bypass` é ignorado quando a política vem de diretiva de máquina — nenhum `.ps1`
    do pendrive executa.
  - Arquivo solto ao lado do executável falha em uso real: agente, `appsettings.json` e o XML da tarefa
    vão como **recursos embutidos** (o `payload.zip` é montado e apagado por `deploy/gerar-pendrive.ps1`).
  - `schtasks /xml` exige o arquivo em **UTF-16 LE**; e a checagem de serviço em execução usa o código
    numérico `: 4` do `sc query`, porque o texto do estado é traduzido conforme o idioma do Windows.
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
- **Agente nas máquinas:** por GPO/AD, ou pelo instalador de pendrive
  (`deploy/gerar-pendrive.ps1` gera um `Instalar-Agente.exe` autocontido de ~79 MB).
  O `.exe` **não é versionado**: carrega a `EnrollmentKey` embutida, então é artefato de build,
  entregue em pendrive — nunca em release do GitHub.

## Nunca versionar

Segredos (tokens, chaves, `.pfx`), o site compilado (`src/Tracker.Api/wwwroot/`), uploads
(`src/Tracker.Api/App_Data/`), `token.dat`, `agent.log`, `bin/`, `obj/`, `node_modules/`, `publish/`.
Já cobertos pelo `.gitignore`. Mantenha `EnrollmentKey`/tokens **vazios** nos `appsettings.json` versionados.

## Verificação ao terminar mudanças

- Backend: `dotnet build Tracker.sln` e `dotnet test Tracker.sln`.
- Frontend: `npm --prefix src/Tracker.Web run build` (faz type-check).
- Se mexeu no agente: rode em console com `Simular=true` para não travar a própria tela.
