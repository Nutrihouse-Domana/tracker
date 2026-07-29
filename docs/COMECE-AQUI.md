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
📖 Detalhes: **deploy-servidor-onpremises.md** (Parte 1).

### 2. Configurar o servidor 192.168.0.254
Na ordem: SQL Server (reaproveitar se já houver) → .NET 8 Hosting Bundle → variáveis de ambiente
(segredos) → criar o site no IIS (binding 443 + host `tracker.foodsacademy.com.br` + SNI, App Pool
próprio) → certificado HTTPS.
📖 Guia completo: **deploy-servidor-onpremises.md** (Partes 2 a 5).

### 3. DNS (sem mexer no firewall)
Na zona `foodsacademy.com.br`, adicionar registro **A**: `tracker` → `170.80.51.154`.
📖 **deploy-servidor-onpremises.md** (Parte 6).

### 4. Testar
De fora da rede (4G): abrir `https://tracker.foodsacademy.com.br/health` → `{"status":"ok"}`.
E confirmar que o FOOD-ACADEMY continua normal.
📖 **deploy-servidor-onpremises.md** (Parte 7).

### 5. Instalar o agente nos notebooks
Apontar `BackendBaseUrl = https://tracker.foodsacademy.com.br`, republicar e instalar como serviço.
📖 Guia do técnico: **teste-agente-windows.md**. Referência técnica: **agents/Tracker.Agent.Windows/README.md**.

### 6. Segurança (obrigatório — está exposto à internet)
Trocar as chaves de teste por novas, senha forte no admin, backup do banco, manter o servidor atualizado.
📖 **deploy-servidor-onpremises.md** (Parte 9).

---

## Guias que você tem

| Arquivo | Para quê |
|---|---|
| **COMECE-AQUI.md** | Este roteiro (índice geral) |
| **deploy-servidor-onpremises.md** | Passo a passo completo do servidor (IIS, HTTPS, SQL, DNS) |
| **teste-agente-windows.md** | Guia para os técnicos instalarem/testarem o agente nos notebooks |
| `deploy/publicar-servidor.ps1` | Gera o pacote da plataforma (site + API) com um comando |
| `deploy/instalar-agente.ps1` / `desinstalar-agente.ps1` | Instalam/removem o agente como serviço |
| `agents/Tracker.Agent.Windows/README.md` | Referência técnica do agente |

---

## Resumo de "quem faz o quê"

- **Você / dev:** roda o `publicar-servidor.ps1`, entrega o pacote.
- **Equipe de infra/redes:** configura o servidor `.254` (IIS, cert, SQL), adiciona o DNS. **Não precisa mexer no FortiGate.**
- **Técnicos:** instalam o agente nos notebooks apontando para `https://tracker.foodsacademy.com.br`.
