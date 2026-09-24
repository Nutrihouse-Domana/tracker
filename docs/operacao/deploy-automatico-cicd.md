# Deploy automático (CI/CD) para produção

Automatiza a publicação do Tracker em produção quando você cria uma **tag de versão** (ex.: `v1.2.0`).
Usa **GitHub Actions** rodando num **self-hosted runner** dentro da rede (no próprio servidor
`192.168.0.254`):

```
Você cria a tag v1.2.0  ->  GitHub Actions (self-hosted runner on-prem)
                              build  ->  dotnet test  ->  gera pacote  ->  publica no IIS
```

> **Por que tudo no runner on-prem?** O repositório é **privado** e o org está no plano **free**,
> então runners GitHub-hosted (`windows-latest`) não ficam disponíveis. O egress da rede bloqueia
> os CDNs de download, então o workflow **não** usa `setup-dotnet`/`setup-node`: o **.NET 8 SDK**
> e o **Node.js** ficam **pré-instalados** no servidor (o passo *Preflight* falha com mensagem se
> faltarem).

- **Build + testes:** se algum teste falhar, o deploy **não acontece**. Os testes rodam contra um
  **banco fake** (instância inexistente): como a migração no start está em `try/catch`
  ([Program.cs](../../src/Tracker.Api/Program.cs)), a API sobe mesmo sem banco e o `/health` passa —
  logo os testes **nunca tocam o banco de produção** configurado na máquina.
- **node_modules:** restaurado de um cache local do servidor (`C:\TrackerCI`), chaveado pelo
  hash do `package-lock.json` — só roda `npm ci` quando o lock muda.
- **Publish + deploy:** gera o pacote site + API e troca os arquivos no IIS (backup, para, espera
  liberar, copia, reinicia, health check). Falha na troca **restaura o backup** sozinha.
- **Instalador do agente Windows:** regerado em `C:\TrackerAgentDist` **só se o agente mudou**
  desde a **última geração bem-sucedida** (roda depois do deploy; falha aqui não desfaz o site).
  Detalhes em [Instalador do agente Windows](#instalador-do-agente-windows).

Arquivos: [`.github/workflows/deploy-producao.yml`](../../.github/workflows/deploy-producao.yml) e
[`deploy/atualizar-servidor.ps1`](../../deploy/atualizar-servidor.ps1).

---

## Por que tag em vez de "todo commit"?

Um commit é local; quem dispara automação é o **push**. Publicar produção a cada push na `main`
é arriscado (as migrações de banco aplicam sozinhas no start). Com **tag**, você controla o momento:

```bash
git checkout main
git pull
git tag v1.2.0
git push origin v1.2.0
```

Isso dispara o pipeline. Para reexecutar sem criar tag nova, use **Actions → Deploy Producao → Run workflow**.

---

## Configuração (uma vez): instalar o self-hosted runner no servidor

No servidor `192.168.0.254`, como **Administrador**:

1. No GitHub: **Settings → Actions → Runners → New self-hosted runner** (Windows x64).
   Siga os comandos de download que a página mostra (baixa o `actions-runner` numa pasta, ex.: `C:\actions-runner`).

2. Ao rodar o `config.cmd`, informe as **labels** exatamente como o workflow espera:

   ```
   ./config.cmd --url https://github.com/Nutrihouse-Domana/tracker --token <TOKEN_DA_PAGINA> --labels self-hosted,windows,tracker-prod
   ```

3. Instale como **serviço do Windows** para rodar sempre:

   ```
   ./svc.cmd install
   ./svc.cmd start
   ```

4. **Permissões:** o serviço do runner precisa controlar o IIS e gravar em `C:\Tracker`.
   Configure o serviço `actions.runner.*` para rodar com uma conta **Administrador local**
   (Services.msc → propriedades do serviço → aba Logon), ou uma conta com direito de
   parar/iniciar sites do IIS e escrever em `C:\Tracker` e `C:\TrackerBackups`.

> Instale no servidor o **.NET 8 SDK** e o **Node.js LTS** (o CI não os baixa) e **reinicie o
> serviço do runner** depois, para ele enxergar o PATH novo. O servidor também precisa do
> **.NET 8 Hosting Bundle** (requisito do site) e de acesso ao GitHub.

---

## Pré-requisitos no servidor (já devem existir, do deploy manual)

- Site `Tracker` no IIS, apontando para `C:\Tracker`, com app pool próprio (ver
  [deploy-servidor-onpremises.md](deploy-servidor-onpremises.md)).
- Variáveis de ambiente de máquina com os segredos de produção (`ConnectionStrings__TrackerDb`,
  `Encryption__CpfKeyBase64`, etc.). **O CI não mexe nisso** — os segredos ficam só no servidor.
  O passo do instalador do agente lê `Enrollment__CompanyKey` dessa mesma env da máquina.
  Opcionais: `TRACKER_BACKEND_URL` (padrão: URL de produção) e `TRACKER_AGENTE_DIST`
  (padrão: `C:\TrackerAgentDist` — fica **fora do `wwwroot`**, pois o `.exe` embute a chave).

---

## O que o deploy faz no servidor (a cada execução)

Script: [`deploy/atualizar-servidor.ps1`](../../deploy/atualizar-servidor.ps1).

1. **Backup** do **conteúdo** de `C:\Tracker` em `C:\TrackerBackups\Tracker-<data-hora>` (mantém os
   5 últimos). Como é o conteúdo, e não a pasta, restaurar é copiar `backup\*` de volta.
2. Para o **site** e o **app pool** no IIS e **espera de verdade**: o pool chegar a `Stopped` e o
   `Tracker.Api.dll` ser liberado (abre o arquivo com exclusividade em laço), até **60 s** cada.
   `Stop-WebAppPool` só *pede* a parada; sem a espera, a cópia pegava a DLL ainda presa pelo
   `w3wp`. Estourou o prazo: o deploy é abortado sem tocar nos arquivos.
3. Substitui os arquivos de `C:\Tracker` (a pasta de dados `C:\TrackerDados` fica de fora e é
   preservada). **Se a troca falhar, restaura o backup** na hora e o site volta na versão anterior.
4. **Sempre** religa o app pool e o site (num `finally`), mesmo em falha — o site nunca fica parado.
   Falha na parada ou na troca encerra o script com **`exit 1`**, e o job do CI fica vermelho.
5. **Health check** no `/health` do host do binding https (ou `https://localhost/health`). Se não
   responder `{"status":"ok"}`, o script **só avisa** (SNI/loopback pode impedir o teste local com
   o app saudável): confira a URL pública e, se estiver fora, faça o rollback manual abaixo.

---

## Instalador do agente Windows

O último passo do workflow decide se regera o `Instalar-Agente.exe` em `C:\TrackerAgentDist`
(ou `TRACKER_AGENTE_DIST`). Regera quando **qualquer** uma destas vale:

- `Instalar-Agente.exe` não existe no destino;
- o marcador `ultima-geracao.txt` não existe, está vazio ou aponta para um commit que não está no
  histórico;
- há diferença entre o commit do marcador e o commit da release em `agents/Tracker.Agent.Windows`,
  `deploy/gerar-pendrive.ps1` ou `deploy/Instalador`.

O `ultima-geracao.txt` tem três linhas — commit, tag e data/hora da geração — e é gravado **por
último**, só depois de o `.exe` e a cópia versionada (`Instalar-Agente-<tag>.exe`) saírem. Antes,
o diff era contra a **tag anterior**: se uma release falhava antes de gerar o agente, a seguinte
via "sem mudança" e o instalador ficava velho para sempre. **No primeiro deploy depois dessa
mudança o marcador ainda não existe, então o agente é regerado uma vez.**

**Forçar a regeração** (ex.: depois de trocar o `Enrollment__CompanyKey`, que não muda código):
apague `C:\TrackerAgentDist\ultima-geracao.txt` antes da próxima tag.

---

## Rollback manual

A troca de arquivos já restaura o backup sozinha quando **ela** falha. Este procedimento é para
quando o deploy terminou, mas a versão nova está com problema:

```powershell
# no servidor, como Administrador
Import-Module WebAdministration
Stop-Website -Name Tracker
$ultimo = Get-ChildItem C:\TrackerBackups -Directory | Sort-Object CreationTime -Desc | Select-Object -First 1
Get-ChildItem C:\Tracker -Force | Remove-Item -Recurse -Force
Copy-Item "$($ultimo.FullName)\*" C:\Tracker -Recurse -Force
Start-Website -Name Tracker
```

> Atenção: rollback de **arquivos** não desfaz **migrações de banco** já aplicadas. Mantenha o
> **backup diário do `TrackerDb`** (ver guia on-premises, Parte 9) para conseguir voltar o schema.

---

## Checklist antes da primeira tag

- [ ] .NET 8 SDK e Node.js LTS instalados no servidor (serviço do runner reiniciado depois).
- [ ] Runner self-hosted instalado, com labels `self-hosted,windows,tracker-prod`, rodando como serviço.
- [ ] Conta do serviço com permissão de IIS + escrita em `C:\Tracker` e `C:\TrackerBackups`.
- [ ] Site `Tracker` e variáveis de ambiente de produção já configurados.
- [ ] Um deploy manual já funcionou pelo menos uma vez (valida IIS + banco + certificado).
- [ ] `Enrollment__CompanyKey` definido na env da máquina do runner (o passo do agente depende dela).
