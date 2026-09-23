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

### 5. Instalar o agente nos aparelhos
- **Notebooks Windows:** a TI gera (ou pega do CI, em `C:\TrackerAgentDist`) o
  **`Instalar-Agente.exe`**, que já vem com o endereço de produção e a chave da empresa embutidos.
  No notebook: duplo clique, credencial de administrador e pronto — o aparelho aparece no
  inventário em até 1 minuto.
  📖 **instalacao-agente-pendrive.md**. Roteiro de teste: **teste-agente-windows.md**.
  Referência técnica: **agents/Tracker.Agent.Windows/README.md**.
- **Celulares e tablets Android:** APK de release gerado pelo CI e instalado como Device Admin
  (aparelho em uso) ou Device Owner (aparelho zerado). Atualizações seguintes chegam por OTA,
  publicadas na página **Agente** do painel.
  📖 **agente-android.md**.

### 6. Segurança (obrigatório — está exposto à internet)
Trocar as chaves de teste por novas, senha forte no admin, backup do banco, manter o servidor atualizado.
📖 **deploy-servidor-onpremises.md** (Parte 9) e **backup-e-chaves.md** (script de backup, teste
de restauração e cofre das chaves).

### 7. Atualizações futuras
Com o CI configurado, publicar uma versão nova é criar uma tag `v*` na `main` — o pipeline
compila, testa, faz backup, publica no IIS e regera o instalador do agente se ele mudou.
📖 **deploy-automatico-cicd.md** (inclui o rollback).

---

## Guias que você tem

| Arquivo | Para quê |
|---|---|
| **COMECE-AQUI.md** | Este roteiro (índice geral) |
| **manual-do-usuario.md** | Como usar o painel no dia a dia (para a equipe de TI) |
| **melhorias-pendentes.md** | Limitações conhecidas do sistema e como corrigi-las |
| **backup-e-chaves.md** | Backup e restauração do banco, cofre das chaves, troca de chave |
| **deploy-servidor-onpremises.md** | Passo a passo completo do servidor (IIS, HTTPS, SQL, DNS) |
| **deploy-automatico-cicd.md** | Deploy automático por tag, runner self-hosted e rollback |
| **instalacao-agente-pendrive.md** | Instalar o agente Windows com o `Instalar-Agente.exe` |
| **teste-agente-windows.md** | Guia para os técnicos testarem o agente nos notebooks |
| **agente-android.md** | Agente Android: APK, Device Admin/Owner, OTA, keystore, diagnóstico |
| **ambiente-dev.md** | Preparar uma máquina de desenvolvimento (banco em container) |
| `deploy/publicar-servidor.ps1` | Gera o pacote da plataforma (site + API) com um comando |
| `deploy/gerar-pendrive.ps1` | Gera o `Instalar-Agente.exe` (agente Windows autocontido) |
| `deploy/gerar-apk-agente.ps1` | Gera o APK de release do agente Android na máquina local |
| `deploy/instalar-agente.ps1` / `desinstalar-agente.ps1` | Instalam/removem o agente como serviço (alternativa ao `.exe`) |
| `agents/Tracker.Agent.Windows/README.md` | Referência técnica do agente Windows |

---

## Resumo de "quem faz o quê"

- **Você / dev:** roda o `publicar-servidor.ps1`, entrega o pacote.
- **Equipe de infra/redes:** configura o servidor `.254` (IIS, cert, SQL), adiciona o DNS. **Não precisa mexer no FortiGate.**
- **Técnicos:** instalam o agente nos notebooks (`Instalar-Agente.exe`) e nos celulares/tablets (APK).
