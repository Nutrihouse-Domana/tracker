# Tracker — Rastreabilidade de Ativos de TI (Nutrihouse)

Plataforma para rastrear notebooks, celulares, tablets, periféricos e impressoras corporativos:
quem está com cada equipamento, onde está, histórico de movimentação/reparos, termo de uso,
controle de equipamentos alugados, relatórios, **monitoramento das impressoras por SNMP** e
**bloqueio remoto** dos dispositivos (via agente instalado).

Produção: `https://tracker.foodsacademy.com.br` (on-premises, Windows + IIS).

## Estrutura do repositório

```
src/
  Tracker.Domain/          Entidades e regras de domínio
  Tracker.Application/     DTOs, validações, casos de uso
  Tracker.Infrastructure/  EF Core (SQL Server), Identity, PDF/XLSX, criptografia, storage, SNMP
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
Guia completo: [docs/desenvolvimento/ambiente-dev.md](docs/desenvolvimento/ambiente-dev.md).

Antes de abrir PR: `dotnet build Tracker.sln`, `dotnet test Tracker.sln` e
`npm --prefix src/Tracker.Web run build`.

## Documentação

Tudo fica em [`docs/`](docs/), separado por assunto. Comece pelo índice
[docs/COMECE-AQUI.md](docs/COMECE-AQUI.md).

| Pasta | Conteúdo |
|---|---|
| [`docs/uso/`](docs/uso/) | [Manual do usuário](docs/uso/manual-do-usuario.md) do painel |
| [`docs/operacao/`](docs/operacao/) | [Servidor on-premises](docs/operacao/deploy-servidor-onpremises.md), [deploy por tag (CI/CD)](docs/operacao/deploy-automatico-cicd.md), [backup e chaves](docs/operacao/backup-e-chaves.md), [recuperação de desastre](docs/operacao/recuperacao-de-desastre.md), [monitoramento de impressoras (SNMP)](docs/operacao/monitoramento-impressoras-snmp.md) |
| [`docs/agentes/`](docs/agentes/) | [Instalação por pendrive](docs/agentes/instalacao-agente-pendrive.md), [teste do agente Windows](docs/agentes/teste-agente-windows.md), [atualização pela GPO](docs/agentes/atualizacao-agente-gpo.md), [agente Android](docs/agentes/agente-android.md), [protocolo servidor ↔ agente](docs/agentes/protocolo-agente.md) |
| [`docs/desenvolvimento/`](docs/desenvolvimento/) | [Ambiente de dev](docs/desenvolvimento/ambiente-dev.md), [melhorias pendentes](docs/desenvolvimento/melhorias-pendentes.md) |
| [`docs/privacidade/`](docs/privacidade/) | [Dados pessoais e LGPD](docs/privacidade/privacidade-lgpd.md) (para o jurídico/DPO) |

Na raiz: [CHANGELOG.md](CHANGELOG.md) (versões) e [CLAUDE.md](CLAUDE.md) (convenções e
pegadinhas). Junto do código: [agente Windows](agents/Tracker.Agent.Windows/README.md) e
[front-end](src/Tracker.Web/README.md).

## Segurança

- CPF criptografado em repouso (AES-GCM); segredos fora do código (user-secrets em dev,
  variáveis de ambiente em produção). Não versione tokens, chaves, certificados nem a keystore
  do APK.
- Ações sensíveis pedem a senha do ADM logado; toda escrita é registrada na auditoria.
