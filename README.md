# Tracker — Rastreabilidade de Ativos de TI (Nutrihouse)

Plataforma para rastrear notebooks, celulares, tablets e periféricos corporativos: quem está com
cada equipamento, onde está, histórico de movimentação/reparos, termo de uso, controle de
equipamentos alugados, relatórios e **bloqueio remoto** dos dispositivos (via agente instalado).

Produção: `https://tracker.foodsacademy.com.br` (on-premises, Windows + IIS).

## Estrutura do repositório

```
src/
  Tracker.Domain/          Entidades e regras de domínio
  Tracker.Application/     DTOs, validações, casos de uso
  Tracker.Infrastructure/  EF Core (SQL Server), Identity, PDF/XLSX, criptografia, storage
  Tracker.Api/             API ASP.NET Core + SignalR (serve também o site em produção)
  Tracker.Web/             Front-end React + TypeScript (Vite, MUI)
agents/
  Tracker.Agent.Windows/   Agente Windows (serviço): heartbeat, localização, bloqueio remoto
  Tracker.Agent.Android/   Agente Android (Kotlin): mesmo protocolo, bloqueio por sobreposição
deploy/                    Scripts de ambiente dev, publicação, instalador do agente e APK
docs/                      Guias de implantação, desenvolvimento e agentes
tests/                     Testes automatizados (xUnit)
.github/workflows/         Deploy de produção (tag v*) e APK de release do agente Android
```

## Stack

- **Backend:** .NET 8 (ASP.NET Core), EF Core, SQL Server, ASP.NET Core Identity, SignalR
- **Frontend:** React + TypeScript (Vite, MUI, TanStack Query, react-leaflet)
- **Agente Windows:** .NET 8 Worker Service
- **Agente Android:** Kotlin (Device Admin / Device Owner, atualização OTA pelo painel)

## Como rodar em desenvolvimento

Pré-requisitos: Windows, .NET 8 SDK, Node.js LTS e Docker Desktop (com WSL2) — ou um SQL Server
Express local no lugar do Docker. Para o agente Android: JDK 17 e Android SDK.

```powershell
# Uma vez por máquina: sobe o SQL Server em container e grava os user-secrets
.\deploy\preparar-ambiente-dev.ps1
```

```bash
# Backend (API em http://localhost:5171; aplica as migrações no start)
dotnet run --project src/Tracker.Api --urls http://localhost:5171

# Frontend (site em http://localhost:5173)
npm --prefix src/Tracker.Web run dev
```

Segredos de desenvolvimento ficam em `dotnet user-secrets` do `Tracker.Api` (não versionados):
`Encryption:CpfKeyBase64`, `Enrollment:CompanyKey`, `Seed:AdmEmail`, `Seed:AdmSenha`, `Seed:AdmNome`.
Guia completo: [docs/ambiente-dev.md](docs/ambiente-dev.md).

Antes de abrir PR: `dotnet build Tracker.sln`, `dotnet test Tracker.sln` e
`npm --prefix src/Tracker.Web run build`.

## Documentação

| Guia | Para quê |
|---|---|
| [docs/COMECE-AQUI.md](docs/COMECE-AQUI.md) | Índice geral e roteiro de implantação |
| [docs/manual-do-usuario.md](docs/manual-do-usuario.md) | Uso do painel pela TI: cadastro, entrega, termo, bloqueio, relatórios, acessos |
| [docs/melhorias-pendentes.md](docs/melhorias-pendentes.md) | Backlog de limitações conhecidas, com local no código e correção sugerida |
| [docs/backup-e-chaves.md](docs/backup-e-chaves.md) | Backup/restauração do banco e guarda das chaves (CPF, agentes, keystore) |
| [docs/ambiente-dev.md](docs/ambiente-dev.md) | Preparar uma máquina de desenvolvimento |
| [docs/deploy-servidor-onpremises.md](docs/deploy-servidor-onpremises.md) | Servidor de produção (IIS, HTTPS, SQL, DNS, segredos) |
| [docs/deploy-automatico-cicd.md](docs/deploy-automatico-cicd.md) | Deploy por tag `v*` e rollback |
| [docs/instalacao-agente-pendrive.md](docs/instalacao-agente-pendrive.md) | Instalar o agente Windows nos notebooks (`Instalar-Agente.exe`) |
| [docs/teste-agente-windows.md](docs/teste-agente-windows.md) | Roteiro de teste do agente Windows |
| [agents/Tracker.Agent.Windows/README.md](agents/Tracker.Agent.Windows/README.md) | Referência técnica do agente Windows |
| [docs/agente-android.md](docs/agente-android.md) | Agente Android: compilar, instalar, Device Owner, OTA, keystore |
| [CLAUDE.md](CLAUDE.md) | Convenções do código e pegadinhas já resolvidas |

## Segurança

- CPF criptografado em repouso (AES-GCM); segredos fora do código (user-secrets em dev,
  variáveis de ambiente em produção). Não versione tokens, chaves, certificados nem a keystore
  do APK.
- Ações sensíveis pedem a senha do ADM logado; toda escrita é registrada na auditoria.
