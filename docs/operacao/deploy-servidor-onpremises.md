# Implantação no Servidor On-Premises (Windows) + Acesso pela Internet

Guia para hospedar a plataforma Tracker num servidor Windows da empresa e deixá-la acessível
de qualquer lugar via `https://tracker.foodsacademy.com.br` (domínio + IP público fixo + IIS como
proxy reverso com HTTPS). Assim os agentes nos notebooks alcançam a plataforma de qualquer rede.

> **Arquitetura:** um único aplicativo ASP.NET Core serve **o site e a API** no mesmo endereço.
> O IIS fica na frente cuidando do HTTPS. O SQL Server roda no mesmo servidor (ou em outro da rede).

> **CENÁRIO DA NUTRIHOUSE (co-hospedagem):** a porta 443 já está publicada no FortiGate
> (`170.80.51.154:443 → 192.168.0.254`), onde roda o site FOOD-ACADEMY em IIS. O Tracker vai
> **no mesmo servidor `192.168.0.254`**, como um **site IIS separado**, reaproveitando a mesma 443
> via **SNI (host name)**. Consequências:
> - **NÃO é preciso mexer no FortiGate** (a Parte 6 vira só o registro de DNS).
> - O FOOD-ACADEMY fica intacto (cada site responde pelo seu próprio host name).
> - Use **App Pool próprio** para o Tracker (isola os dois).

---

## Visão geral do fluxo

```
Notebook (agente)  →  Internet  →  [Roteador: porta 443 → servidor]  →  IIS (HTTPS)  →  App .NET (Kestrel) → SQL Server
Navegador (TI)     →  https://tracker.foodsacademy.com.br  ↑ (mesma coisa)
```

---

## Parte 1 — Preparar o pacote (na máquina de build)

Na máquina que tem **.NET 8 SDK + Node.js** (a de desenvolvimento):

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\publicar-servidor.ps1
```

Isso gera `C:\publish\Tracker` com o site + API juntos. **Copie essa pasta para o servidor**
(ex.: `C:\Tracker`).

---

## Parte 2 — No servidor: pré-requisitos

### 2.1 SQL Server
- **Se o servidor `192.168.0.254` já tem SQL Server** (é provável, se o FOOD-ACADEMY usa): **reaproveite
  a instância existente** — não precisa instalar outro. Só criará um banco novo (`TrackerDb`) e um login.
- Se não tiver: instale SQL Server Express (grátis) ou Standard. Anote a instância (ex.: `SQLEXPRESS`).
- Crie o banco **vazio** e um login para a aplicação (recomendado, em vez de usar `sa`), **antes**
  do primeiro start:
  ```sql
  CREATE DATABASE TrackerDb;
  CREATE LOGIN tracker_app WITH PASSWORD = 'SENHA_FORTE_AQUI';
  USE TrackerDb;
  CREATE USER tracker_app FOR LOGIN tracker_app;
  ALTER ROLE db_owner ADD MEMBER tracker_app;
  ```
  No start a aplicação cria as tabelas (migrações) com esse login. O `tracker_app` não tem
  permissão de `CREATE DATABASE`: sem o banco criado antes, a migração falha e o erro **não derruba
  o site** — aparece só um aviso no log e as telas dão erro de banco.

### 2.2 IIS + o .NET 8 Hosting Bundle
- O IIS **já está instalado** (roda o FOOD-ACADEMY). Só falta o runtime do .NET:
1. Instale o **.NET 8 Hosting Bundle** (runtime ASP.NET Core + módulo do IIS):
   `https://dotnet.microsoft.com/download/dotnet/8.0` → "Hosting Bundle".
2. Reinicie o IIS: no PowerShell (Admin) `net stop was /y` e `net start w3svc`.
   > Isso reinicia o IIS inteiro (afeta o FOOD-ACADEMY por alguns segundos) — faça em horário de baixo uso.

> Para o deploy **manual** basta o Hosting Bundle. Com o **CI** (o runner roda neste servidor),
> instale também o .NET 8 SDK e o Node.js LTS — ver [deploy-automatico-cicd.md](deploy-automatico-cicd.md).

---

## Parte 3 — Configurar os segredos (variáveis de ambiente do sistema)

Em produção os segredos vão em **variáveis de ambiente da máquina** (não em arquivo). No servidor,
PowerShell **como Administrador**:

```powershell
# String de conexão (ajuste instância, login e senha)
[Environment]::SetEnvironmentVariable("ConnectionStrings__TrackerDb",
  "Server=localhost\SQLEXPRESS;Database=TrackerDb;User Id=tracker_app;Password=SENHA_FORTE_AQUI;TrustServerCertificate=True", "Machine")

# Chave de criptografia do CPF (GERE UMA NOVA para produção — não use a de teste)
$bytes = New-Object byte[] 32; (New-Object System.Security.Cryptography.RNGCryptoServiceProvider).GetBytes($bytes)
[Environment]::SetEnvironmentVariable("Encryption__CpfKeyBase64", [Convert]::ToBase64String($bytes), "Machine")

# Chave da empresa para auto-cadastro dos agentes (troque por uma nova, forte)
[Environment]::SetEnvironmentVariable("Enrollment__CompanyKey", "TROQUE-POR-UMA-CHAVE-FORTE", "Machine")

# Primeiro usuário ADM (criado no primeiro start)
[Environment]::SetEnvironmentVariable("Seed__AdmEmail", "admin@nutrihouse.com.br", "Machine")
[Environment]::SetEnvironmentVariable("Seed__AdmSenha", "UMA-SENHA-FORTE-DE-ADMIN", "Machine")
[Environment]::SetEnvironmentVariable("Seed__AdmNome",  "Administrador TI", "Machine")

# Onde guardar os termos de uso assinados (pasta com backup)
[Environment]::SetEnvironmentVariable("Storage__TermosDeUsoPath", "C:\TrackerDados\termos-de-uso", "Machine")

# Onde guardar os APKs do agente Android publicados para OTA. OBRIGATORIO ficar fora de
# C:\Tracker: o deploy apaga a pasta do site inteira a cada release.
[Environment]::SetEnvironmentVariable("Storage__PacotesAgentePath", "C:\TrackerDados\pacotes-agente", "Machine")

# Community SNMP das impressoras (a mesma configurada nelas). Sem ela vale "public".
# Detalhes: monitoramento-impressoras-snmp.md
[Environment]::SetEnvironmentVariable("Snmp__Community", "<CODIGO-DO-COFRE>", "Machine")
```

> ⚠️ **Anote a `Encryption__CpfKeyBase64`** num cofre de senhas. Se ela for perdida/trocada, os
> CPFs já gravados não poderão mais ser descriptografados.

---

## Parte 4 — Criar o site no IIS (ao lado do FOOD-ACADEMY)

1. Abra o **Gerenciador do IIS** → clique com o direito em **Sites → Adicionar Site**.
   - Nome do site: `Tracker`
   - Caminho físico: `C:\Tracker` (a pasta publicada que você copiou)
   - **Pool de Aplicativos**: crie um **novo** (não use o do FOOD-ACADEMY) — isola os dois.
   - Vinculação: **https**, porta **443**, **Host name `tracker.foodsacademy.com.br`**,
     **marque "Exigir Indicação de Nome do Servidor (SNI)"** e selecione o certificado (Parte 5).
2. No **Pool de Aplicativos** do site (`Tracker`): defina **.NET CLR = "Sem Código Gerenciado"**
   (o app roda via módulo ASP.NET Core, não pelo CLR do IIS).
3. Dê permissão de leitura da pasta `C:\Tracker` e escrita em `C:\TrackerDados` para a identidade
   do pool (`IIS AppPool\Tracker`).

> **Importante (não quebrar o FOOD-ACADEMY):** o SNI faz cada site responder pelo seu próprio host
> name na mesma 443. Confirme que a vinculação 443 do FOOD-ACADEMY também tem um **host name definido**
> (não em branco). Com os dois com host name + SNI, eles coexistem sem conflito.

---

## Parte 5 — HTTPS (certificado)

Escolha uma das opções:

**a) Let's Encrypt (grátis, automático)** — recomendado se a porta 80/443 estiver acessível de fora:
- Baixe o **win-acme** (`https://www.win-acme.com/`), rode `wacs.exe`, escolha o site `Tracker`
  e o host `tracker.foodsacademy.com.br`. Ele emite o certificado, cria a vinculação **https (443)**
  e renova sozinho.

**b) Certificado da empresa** (comprado ou da CA interna):
- Importe o `.pfx` no servidor (Gerenciador de Certificados) e no IIS adicione uma **vinculação
  https (443)** ao site `Tracker` usando esse certificado.

Depois, no site, mantenha a vinculação **https 443** com o host `tracker.foodsacademy.com.br`.

---

## Parte 6 — DNS e roteador

1. **DNS:** crie um registro **A** `tracker.foodsacademy.com.br` → **IP público fixo** da empresa.
2. **Roteador/Firewall da empresa:** encaminhe a porta **443 (TCP)** da internet para o **IP interno
   do servidor**.
3. **Firewall do Windows** no servidor: permita a porta 443 de entrada.
4. **Impressoras (SNMP):** o servidor precisa alcançar as impressoras na **UDP 161** de saída,
   inclusive nas filiais. Ver [monitoramento-impressoras-snmp.md](monitoramento-impressoras-snmp.md).

---

## Parte 7 — Testar

- No servidor: `https://localhost/health` (aceite o aviso de certificado se for localhost).
- De **fora da rede** (ex.: 4G no celular): abrir `https://tracker.foodsacademy.com.br/health` →
  deve retornar `{"status":"ok"}`. E abrir a raiz deve mostrar a tela de login.

---

## Parte 8 — Apontar os agentes para o domínio

O endereço de produção com **https** e a chave da empresa (Parte 3) vão **embutidos** no
instalador do agente Windows — não se edita `appsettings.json` à mão em cada notebook.

Com o CI configurado, use o `.exe` que ele regera sozinho em `C:\TrackerAgentDist` sempre que uma
release altera o agente. Para gerar à mão, passe a chave de **produção** (do cofre):

```powershell
.\deploy\gerar-pendrive.ps1 -Destino E:\TrackerAgent -BackendUrl https://tracker.foodsacademy.com.br -EnrollmentKey "<CODIGO-DO-COFRE>"
```

Sem `-EnrollmentKey`, o script lê a chave do user-secrets da máquina — numa máquina de dev, é a
chave de **dev**, e o instalador sairia recusado (401) pela produção. Instalação nos notebooks: `docs/agentes/instalacao-agente-pendrive.md`.
Agente Android: a URL de produção vem do `gradle.properties` no build de release
(`docs/agentes/agente-android.md`).

Assim os agentes funcionam **de qualquer rede**, com o endereço fixo e HTTPS.

---

## Parte 9 — Segurança (obrigatório, já que está exposto à internet)

- **Troque as chaves de teste** por novas em produção (`Encryption__CpfKeyBase64`, `Enrollment__CompanyKey`)
  e use uma **senha forte** para o ADM inicial.
- Mantenha o **Windows Server e o Hosting Bundle atualizados**.
- Deixe no firewall **apenas a 443** aberta para a internet (bloqueie o resto).
- Faça **backup diário do banco** `TrackerDb` e da pasta de dados (`C:\TrackerDados`), e guarde as
  chaves no cofre. Script, teste de restauração e o que fazer com cada chave:
  [backup-e-chaves.md](backup-e-chaves.md).
- Considere **BitLocker** nos notebooks (protege o dado em caso de roubo, complementando o bloqueio).

---

## Parte 10 — Atualizar a aplicação no futuro

**Caminho normal (automático):** crie uma tag de versão na `main` — o pipeline compila, roda os
testes, faz backup de `C:\Tracker`, publica no IIS e confere o `/health`. O script
(`deploy/atualizar-servidor.ps1`) espera o app pool parar e a DLL ser liberada (até 60 s), restaura
o backup sozinho se a troca de arquivos falhar e religa pool e site mesmo em falha:

```bash
git checkout main
git pull
git tag v1.2.0
git push origin v1.2.0
```

Configuração do runner, rollback e o critério de regeração do instalador do agente Windows
(marcador `C:\TrackerAgentDist\ultima-geracao.txt`):
[deploy-automatico-cicd.md](deploy-automatico-cicd.md).

**Caminho manual** (runner indisponível):

1. Na máquina de build: rode `deploy\publicar-servidor.ps1` de novo.
2. No servidor, o jeito mais seguro é rodar o mesmo script do CI, que faz backup, espera o pool
   parar, troca os arquivos com rollback automático e religa tudo:
   `.\deploy\atualizar-servidor.ps1 -Origem C:\publish\Tracker` (como Administrador).
3. Sem o script: no IIS, **pare o site e o app pool** `Tracker` e espere o pool aparecer como
   *Parado* antes de copiar (senão o `Tracker.Api.dll` continua em uso). Substitua os arquivos em
   `C:\Tracker` pela nova publicação (preserve a pasta de dados, que fica em `C:\TrackerDados`).
4. **Inicie o app pool e o site**. As migrações de banco novas são aplicadas sozinhas no start.

> Em qualquer caminho, as migrações não voltam atrás com o rollback de arquivos — tenha o backup
> do `TrackerDb` do dia antes de publicar.

---

## Resolução de problemas

| Sintoma | Verifique |
|---|---|
| Site não abre localmente | Hosting Bundle instalado? Pool "Sem Código Gerenciado"? Veja o Visualizador de Eventos → Aplicativo |
| Erro 500.19 / 500.30 no IIS | Hosting Bundle ausente/desatualizado; reinstale e reinicie o IIS |
| Não conecta no banco | Variável `ConnectionStrings__TrackerDb`, SQL Server ativo, login/senha e permissão do login no `TrackerDb` |
| Abre local mas não de fora | DNS apontando pro IP certo? Porta 443 encaminhada no roteador e liberada no firewall? |
| Agente não conecta | `BackendBaseUrl` com `https://` e domínio certo; testar `/health` no navegador do notebook |

Logs do app: Visualizador de Eventos do Windows (origem ".NET Runtime"/"IIS AspNetCore Module"), ou
ative `stdout` no `web.config` temporariamente para depuração.
