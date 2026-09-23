# Comece aqui — Roteiro de implantação do Tracker

Este é o índice que amarra tudo na ordem. Cada etapa aponta para o guia detalhado.

---

## Valores para ter em mãos antes de começar

| Item | Valor |
|---|---|
| Domínio do Tracker | `tracker.foodsacademy.com.br` |
| IP público (já publicado) | `170.80.51.154` |
| Servidor onde vai rodar | `192.168.0.254` (mesmo do FOOD-ACADEMY, Windows + IIS) |
| Porta | `443` (reaproveitada — **sem mexer no FortiGate**) |
| Chave de criptografia do CPF | *gerar nova na hora (guia de deploy, Parte 3)* — **guardar no cofre** |
| Chave da empresa (agentes) | *definir uma forte* — ex.: `______` |
| Senha do admin inicial | *definir uma forte* |
| Certificado HTTPS | wildcard `*.foodsacademy.com.br`, ou um só pro subdomínio, ou SAN no cert atual |

---

## Ordem de execução

### 1. Gerar o pacote da plataforma (na máquina de build — a de desenvolvimento)
Tem .NET 8 SDK + Node.js. Rode:
```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\publicar-servidor.ps1
```
➡️ Gera `C:\publish\Tracker`. Copie essa pasta para o servidor `192.168.0.254`.
📖 Detalhes: [deploy-servidor-onpremises.md](operacao/deploy-servidor-onpremises.md) (Parte 1).

### 2. Configurar o servidor 192.168.0.254
Na ordem: SQL Server (reaproveitar se já houver) → .NET 8 Hosting Bundle → variáveis de ambiente
(segredos) → criar o site no IIS (binding 443 + host `tracker.foodsacademy.com.br` + SNI, App Pool
próprio) → certificado HTTPS.
📖 Guia completo: [deploy-servidor-onpremises.md](operacao/deploy-servidor-onpremises.md) (Partes 2 a 5).

### 3. DNS (sem mexer no firewall)
Na zona `foodsacademy.com.br`, adicionar registro **A**: `tracker` → `170.80.51.154`.
📖 [deploy-servidor-onpremises.md](operacao/deploy-servidor-onpremises.md) (Parte 6).

### 4. Testar
De fora da rede (4G): abrir `https://tracker.foodsacademy.com.br/health` → `{"status":"ok"}`.
E confirmar que o FOOD-ACADEMY continua normal.
📖 [deploy-servidor-onpremises.md](operacao/deploy-servidor-onpremises.md) (Parte 7).

### 5. Instalar o agente nos aparelhos
- **Notebooks Windows:** a TI gera (ou pega do CI, em `C:\TrackerAgentDist`) o
  **`Instalar-Agente.exe`**, que já vem com o endereço de produção e a chave da empresa embutidos.
  No notebook: duplo clique, credencial de administrador e pronto — o aparelho aparece no
  inventário em até 1 minuto.
  📖 [instalacao-agente-pendrive.md](agentes/instalacao-agente-pendrive.md). Roteiro de teste: [teste-agente-windows.md](agentes/teste-agente-windows.md).
  Referência técnica: [agents/Tracker.Agent.Windows/README.md](../agents/Tracker.Agent.Windows/README.md).
- **Celulares e tablets Android:** APK de release gerado pelo CI e instalado como Device Admin
  (aparelho em uso) ou Device Owner (aparelho zerado). Atualizações seguintes chegam por OTA,
  publicadas na página **Agente** do painel.
  📖 [agente-android.md](agentes/agente-android.md).

### 6. Segurança (obrigatório — está exposto à internet)
Trocar as chaves de teste por novas, senha forte no admin, backup do banco, manter o servidor atualizado.
📖 [deploy-servidor-onpremises.md](operacao/deploy-servidor-onpremises.md) (Parte 9) e [backup-e-chaves.md](operacao/backup-e-chaves.md) (script de backup, teste
de restauração e cofre das chaves).

### 7. Atualizações futuras
Com o CI configurado, publicar uma versão nova é criar uma tag `v*` na `main` — o pipeline
compila, testa, faz backup, publica no IIS e regera o instalador do agente se ele mudou.
📖 [deploy-automatico-cicd.md](operacao/deploy-automatico-cicd.md) (inclui o rollback).

---

## Guias que você tem

A documentação fica em `docs/`, separada por assunto. Este arquivo é o índice.

### `uso/`: quem usa o painel

| Guia | Para quê |
|---|---|
| [manual-do-usuario.md](uso/manual-do-usuario.md) | Como usar o painel no dia a dia (equipe de TI) |

### `operacao/`: servidor de produção

| Guia | Para quê |
|---|---|
| [deploy-servidor-onpremises.md](operacao/deploy-servidor-onpremises.md) | Passo a passo completo do servidor (IIS, HTTPS, SQL, DNS, segredos) |
| [deploy-automatico-cicd.md](operacao/deploy-automatico-cicd.md) | Deploy automático por tag, runner self-hosted e rollback |
| [backup-e-chaves.md](operacao/backup-e-chaves.md) | Backup e restauração do banco, cofre das chaves, troca de chave |
| [recuperacao-de-desastre.md](operacao/recuperacao-de-desastre.md) | Remontar o servidor do zero (ordem, verificação, o que se perde) |

### `agentes/`: notebooks, celulares e tablets

| Guia | Para quê |
|---|---|
| [instalacao-agente-pendrive.md](agentes/instalacao-agente-pendrive.md) | Instalar o agente Windows com o `Instalar-Agente.exe` |
| [teste-agente-windows.md](agentes/teste-agente-windows.md) | Roteiro para os técnicos testarem o agente nos notebooks |
| [agente-android.md](agentes/agente-android.md) | Agente Android: APK, Device Admin/Owner, OTA, keystore, diagnóstico |
| [protocolo-agente.md](agentes/protocolo-agente.md) | Como servidor e agentes conversam (diagnóstico e agente novo) |
| [`agents/Tracker.Agent.Windows/README.md`](../agents/Tracker.Agent.Windows/README.md) | Referência técnica do agente Windows (junto do código) |

### `desenvolvimento/`: quem mexe no código

| Guia | Para quê |
|---|---|
| [ambiente-dev.md](desenvolvimento/ambiente-dev.md) | Preparar uma máquina de desenvolvimento (banco em container) |
| [melhorias-pendentes.md](desenvolvimento/melhorias-pendentes.md) | Backlog de limitações conhecidas e como corrigi-las |
| [`src/Tracker.Web/README.md`](../src/Tracker.Web/README.md) | Organização e regras do front-end (junto do código) |

### `privacidade/`: dados pessoais

| Guia | Para quê |
|---|---|
| [privacidade-lgpd.md](privacidade/privacidade-lgpd.md) | Dados pessoais tratados, terceiros, retenção. Para o jurídico/DPO |

### Na raiz do repositório

Ficam na raiz porque o GitHub e as ferramentas os procuram lá.

| Arquivo | Para quê |
|---|---|
| [`README.md`](../README.md) | Apresentação do projeto |
| [`CHANGELOG.md`](../CHANGELOG.md) | Histórico de versões publicadas |
| [`CLAUDE.md`](../CLAUDE.md) | Convenções do código e pegadinhas já resolvidas |

### Scripts (`deploy/`)

| Script | Para quê |
|---|---|
| `publicar-servidor.ps1` | Gera o pacote da plataforma (site + API) com um comando |
| `gerar-pendrive.ps1` | Gera o `Instalar-Agente.exe` (agente Windows autocontido) |
| `gerar-apk-agente.ps1` | Gera o APK de release do agente Android na máquina local |
| `instalar-agente.ps1` / `desinstalar-agente.ps1` | Instalam/removem o agente como serviço (alternativa ao `.exe`) |
| `preparar-ambiente-dev.ps1` | Prepara uma máquina de desenvolvimento |

---

## Resumo de "quem faz o quê"

- **Você / dev:** roda o `publicar-servidor.ps1`, entrega o pacote.
- **Equipe de infra/redes:** configura o servidor `.254` (IIS, cert, SQL), adiciona o DNS. **Não precisa mexer no FortiGate.**
- **Técnicos:** instalam o agente nos notebooks (`Instalar-Agente.exe`) e nos celulares/tablets (APK).
