# Ambiente de desenvolvimento

Como deixar uma máquina nova pronta para desenvolver o Tracker — trocando de computador ou
passando o projeto para outra pessoa.

> **O projeto exige Windows.** O container resolve o banco, mas o resto não é multiplataforma:
> a geração de PDF lê as fontes do Windows, o agente é um Serviço do Windows e os scripts são
> PowerShell. Em macOS ou Linux isto **não** funciona sem trabalho adicional.

> **Produção não usa Docker.** Lá é SQL Server on-premises no servidor Windows com IIS
> (ver [deploy-servidor-onpremises.md](../operacao/deploy-servidor-onpremises.md)). O container existe
> só para evitar instalar o SQL Server Express na mão em cada máquina de desenvolvimento.

---

## Pré-requisitos

| Ferramenta | Onde obter |
|---|---|
| .NET 8 SDK | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Node.js (LTS) | https://nodejs.org |
| Docker Desktop | https://www.docker.com/products/docker-desktop |
| *(só para o agente Android)* JDK 17 | https://adoptium.net |
| *(só para o agente Android)* Android SDK | API 34 + build-tools 34.0.0 |

O Docker Desktop no Windows usa o **WSL2** como backend. Se a máquina ainda não tem o WSL, o
Docker instala e abre normalmente, mas o *engine* nunca fica pronto — e o sintoma é idêntico ao
de "Docker fechado". Em um PowerShell **como Administrador**:

```powershell
wsl --install
```

Depois **reinicie o Windows** e abra o Docker Desktop uma vez, esperando o status ficar
*Running*. O script de preparação detecta a falta do WSL e avisa com essas instruções.

Depois de instalar, **abra um terminal novo** (o PATH só vale em sessões abertas depois da
instalação).

Se a máquina já tem SQL Server Express instalado e você prefere continuar com ele, o Docker é
dispensável — veja [Sem Docker](#sem-docker) no fim.

---

## Um comando

Na raiz do repositório, em PowerShell:

```powershell
.\deploy\preparar-ambiente-dev.ps1
```

Se aparecer *"execução de scripts foi desabilitada neste sistema"*, é a política de execução do
PowerShell — o padrão do Windows em máquina nova é `Restricted`. Libere **só para esta janela**,
sem alterar a configuração da máquina:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

O script é **idempotente**: rodar de novo não regera nada que já exista. Ele:

1. confere `dotnet`, `node` e `docker`;
2. cria o `.env` com uma senha aleatória para o SA (não versionado);
3. sobe o SQL Server em container e **espera ficar realmente pronto**;
4. grava os `user-secrets` que faltarem no `Tracker.Api`;
5. instala as dependências do site (`npm ci`);
6. configura o agente Android, **se** o SDK estiver instalado.

No fim ele imprime o **login ADM inicial** com uma senha aleatória. Anote: ela não é exibida de
novo. Se perder, redefina sem perder o histórico ligado à conta:

```powershell
dotnet run --project src/Tracker.Api -- --redefinir-senha-adm --senha "NOVA_SENHA"
```

As migrações **não** são aplicadas pelo script — a API as aplica sozinha ao subir.

**O banco nasce vazio.** O seed cria apenas o login ADM e suas permissões; não há dispositivos,
unidades nem detentores de exemplo. Para mexer nas telas de lista, relatório ou aluguel, cadastre
alguns ativos à mão pela própria interface depois do primeiro login.

---

## Rodar o projeto

```powershell
dotnet run --project src/Tracker.Api --urls http://localhost:5171
```

```powershell
npm --prefix src/Tracker.Web run dev
```

API em `http://localhost:5171`, site em `http://localhost:5173`.

---

## Dia a dia

| O que | Comando |
|---|---|
| Parar o banco | `docker compose stop` |
| Subir de novo | `docker compose up -d` |
| Ver o log do banco | `docker logs tracker-db` |
| **Zerar o banco** (apaga os dados) | `docker compose down -v` |

O banco escuta em **`localhost,11433`** — porta 11433 de propósito, porque a 1433 costuma estar
ocupada por um SQL Server já instalado na máquina, e o conflito apareceria só como uma falha de
conexão sem explicação. Para abrir no SQL Server Management Studio ou no Azure Data Studio:

- Servidor: `localhost,11433`
- Autenticação: SQL Server
- Usuário: `sa`
- Senha: a que está no `.env` da raiz

---

## Sem Docker

Para usar um SQL Server Express já instalado na máquina, mantendo a connection string do
`appsettings.json` (`localhost\SQLEXPRESS` com autenticação do Windows):

```powershell
.\deploy\preparar-ambiente-dev.ps1 -SemDocker
```

Ele pula o container e só configura os segredos de desenvolvimento.

Se você já tem um ambiente funcionando e roda o script **com** Docker, a connection string
existente nos `user-secrets` é **preservada** — o script não sequestra um ambiente que já
funciona. Para apontar de fato para o container:

```powershell
.\deploy\preparar-ambiente-dev.ps1 -Forcar
```

E para voltar ao SQL Express local a qualquer momento:

```powershell
dotnet user-secrets remove ConnectionStrings:TrackerDb --project src/Tracker.Api
```

---

## Problemas comuns

**"O Docker está instalado mas não está rodando"** — abra o Docker Desktop e espere o ícone ficar
verde antes de rodar de novo.

**O banco não fica pronto a tempo** — na primeira vez a imagem do SQL Server tem cerca de 1,5 GB
para baixar. Veja o andamento com `docker logs tracker-db`.

**Porta 11433 ocupada** — mude a porta do lado do host em `docker-compose.yml` (`"11433:1433"`) e
ajuste a connection string com `-Forcar`.

**A senha do ADM não aparece** — ela só é exibida quando é criada. Se os `user-secrets` já tinham
`Seed:AdmSenha`, o script preserva o valor antigo e não mostra nada. Use o
`--redefinir-senha-adm` acima.

**Perdi o `.env`** — apague os dados do banco (`docker compose down -v`) e rode o script de novo;
ele gera uma senha nova. Sem o `.env` não há como recuperar a senha do SA do container.

---

## Agente Android

O passo 6 detecta o Android SDK e o JDK 17 e escreve o `local.properties` (fora do Git) com:

- `sdk.dir` — caminho do SDK, com a barra escapada como o formato `.properties` exige;
- `tracker.backendBaseUrl` — `http://<ip-desta-máquina>:5171`.

Se o SDK não estiver instalado, o script **avisa e segue** — quem mexe só no backend e no site não
precisa baixar vários GB. Para pular de propósito: `-SemAndroid`. Se a máquina tiver várias redes e
o IP detectado for o errado: `-IpDev 192.168.0.99`.

```powershell
cd agents/Tracker.Agent.Android
./gradlew :app:assembleDebug
```

> **Por que a URL importa tanto.** Sem `tracker.backendBaseUrl`, o build de **debug** cai para a URL
> de **produção**. E o Android Studio cria o `local.properties` sozinho só com `sdk.dir` ao abrir o
> projeto — ou seja, compilar o debug e instalar num tablet de teste **cadastraria o aparelho no
> inventário real**. É justamente isso que o passo 6 evita.

Para o aparelho alcançar a API, ela precisa escutar fora do `localhost`, a porta 5171 estar liberada
no firewall e o aparelho estar no mesmo Wi-Fi:

```powershell
dotnet run --project src/Tracker.Api --urls http://0.0.0.0:5171
```

**O APK de release sai do CI, não daqui.** Você não precisa da keystore da frota na sua máquina:
na aba **Actions**, rode o workflow **APK Agente Android** e baixe o APK pronto e assinado, com o
`versionCode` e o SHA-256 no resumo do run. O script local não gera keystore de propósito — um APK
assinado com outra chave é recusado pelo Android nos aparelhos já instalados e duplica os registros
do inventário. Ver [agente-android.md](../agentes/agente-android.md).

---

## O que continua fora do container

- **Agente Windows** — é um Serviço do Windows com tarefa agendada, tela de bloqueio e acesso à
  Native Wifi API. Só roda em Windows, na própria máquina. Ver
  [teste-agente-windows.md](../agentes/teste-agente-windows.md).
- **Agente Android (release)** — o **debug** é configurado pelo script (acima); só o **release**
  fica de fora, por causa da keystore. Testar o bloqueio exige um aparelho de laboratório —
  **nunca o próprio celular**, o bloqueio é persistente e só a TI remove. Ver
  [agente-android.md](../agentes/agente-android.md).
- **Geração de PDF** (termo de uso e relatórios) — depende das fontes do Windows (Calibri, com
  Arial como alternativa). Funciona no seu Windows normalmente; só não funcionaria se a API
  fosse rodar dentro de um container Linux, o que não é o caso aqui.
