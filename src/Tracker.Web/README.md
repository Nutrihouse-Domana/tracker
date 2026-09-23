# Tracker.Web — site do Tracker

Front-end React + TypeScript (Vite, MUI v9, TanStack Query, react-leaflet, SignalR). Em
produção é compilado para `src/Tracker.Api/wwwroot` e servido pela própria API, no mesmo
endereço — não existe servidor web separado.

## Comandos (rode da raiz do repositório)

```bash
npm --prefix src/Tracker.Web install     # 1a vez
npm --prefix src/Tracker.Web run dev     # http://localhost:5173
npm --prefix src/Tracker.Web run build   # type-check (tsc -b) + build de produção
npm --prefix src/Tracker.Web run lint    # oxlint
```

O `build` é a verificação obrigatória antes de abrir PR: é ele que faz o type-check.

## Endereço da API

`src/api/client.ts` usa `VITE_API_BASE_URL` (padrão `http://localhost:5171`):

- **Dev:** sobrescreva num `.env.local` (não versionado) se a API estiver em outra porta.
- **Produção:** `.env.production` deixa o valor **vazio** — as chamadas saem relativas ao
  próprio site, que é a API.

O interceptor do axios trata **401 como sessão expirada** e desloga. Por isso confirmações de
senha do ADM devolvem **400** no backend — não mude isso sem mexer nos dois lados.

## Organização

```
src/
  api/        cliente axios, QueryClient e tipos compartilhados
  features/   uma pasta por funcionalidade: api.ts + hooks.ts + componentes
  pages/      telas ligadas às rotas
  layout/     AppShell (barra, menu, busca, alternância de tema)
  utils/      ordenação e paginação de tabelas, formatação, erros da API
  theme.ts    criarTema('light' | 'dark')
```

## Regras que valem para tela nova

- **Cores por token do tema** (`text.primary`, `background.paper`, `divider`…), nunca hex fixo
  de fundo/texto — o site tem modo escuro.
- **Permissões:** esconda (não desabilite) o que o usuário não pode usar, com
  `usePermissao`/`RequirePermissao`/`PermissaoRoute`. A regra real é a policy do backend.
- **Tabelas:** `useOrdenacao` e `usePaginacao` (`src/utils`), aplicados sobre a lista já filtrada.
- **Datas sem hora** (`"2026-10-05"`): não use `new Date(...)` — ele lê como UTC e volta um dia.
- **Identificação do aparelho:** use `identificacaoExibida` (`features/dispositivos/aluguel.ts`).

Detalhes e pegadinhas conhecidas: [CLAUDE.md](../../CLAUDE.md) na raiz.
