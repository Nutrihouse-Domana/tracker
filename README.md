# Tracker — Rastreabilidade de Ativos de TI (Nutrihouse)

Plataforma para rastrear notebooks, telefones e periféricos corporativos: quem está com cada
equipamento, onde está, histórico de movimentação/reparos, termo de uso, e **bloqueio remoto**
dos dispositivos (via agente instalado).

## Estrutura do repositório

```
src/
  Tracker.Domain/          Entidades e regras de domínio
  Tracker.Application/     DTOs, validações, casos de uso
  Tracker.Infrastructure/  EF Core (SQL Server), PDF, segurança, storage
  Tracker.Api/             API ASP.NET Core + SignalR (serve também o site em produção)
  Tracker.Web/             Front-end React + TypeScript (Vite, MUI)
agents/
  Tracker.Agent.Windows/   Agente Windows (serviço): heartbeat, localização, bloqueio remoto
deploy/                    Scripts de publicação e instalação
docs/                      Guias de implantação e testes
tests/                     Testes automatizados (xUnit)
```

## Stack

- **Backend:** .NET 8 (ASP.NET Core), EF Core, SQL Server, ASP.NET Core Identity, SignalR
- **Frontend:** React + TypeScript (Vite, MUI, TanStack Query, react-leaflet)
- **Agente:** .NET 8 Worker Service (Windows)

## Como rodar em desenvolvimento

Pré-requisitos: .NET 8 SDK, Node.js, SQL Server (Express serve).

```bash
# Backend (API em http://localhost:5171)
dotnet run --project src/Tracker.Api

# Frontend (site em http://localhost:5173)
npm --prefix src/Tracker.Web run dev
```

Segredos de desenvolvimento ficam em `dotnet user-secrets` (não versionados):
`Encryption:CpfKeyBase64`, `Enrollment:CompanyKey`, `Seed:AdmEmail`, `Seed:AdmSenha`.

## Implantação e testes

- **Servidor (produção on-premises):** `docs/deploy-servidor-onpremises.md`
- **Roteiro geral:** `docs/COMECE-AQUI.md`
- **Agente nos notebooks:** `agents/Tracker.Agent.Windows/README.md` e `docs/teste-agente-windows.md`

## Segurança

- CPF criptografado em repouso; segredos fora do código (user-secrets em dev, variáveis de
  ambiente em produção). Não versione tokens, chaves ou certificados.
