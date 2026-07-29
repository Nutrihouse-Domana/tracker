# Deploy automático (CI/CD) para produção

Automatiza a publicação do Tracker em produção quando você cria uma **tag de versão** (ex.: `v1.2.0`).
Usa **GitHub Actions** com dois estágios:

```
Você cria a tag v1.2.0  ->  GitHub Actions
                              ├── Estágio 1 (nuvem):  build + dotnet test + gera o pacote
                              └── Estágio 2 (on-prem): baixa o pacote e publica no IIS do servidor
```

- **Estágio 1** roda num runner da nuvem (`windows-latest`). Compila, roda os testes (se algum
  falhar, o deploy **não acontece**) e gera o pacote site + API. Nunca toca em produção.
- **Estágio 2** roda num **self-hosted runner** dentro da rede (no próprio servidor `192.168.0.254`).
  Só faz a troca dos arquivos e reinicia o site — **não compila e não roda testes**, então nunca
  executa nada contra o banco real.

Arquivos: [`.github/workflows/deploy-producao.yml`](../.github/workflows/deploy-producao.yml) e
[`deploy/atualizar-servidor.ps1`](../deploy/atualizar-servidor.ps1).

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

> O servidor **não precisa** de .NET SDK nem Node.js — o pacote já vem pronto da nuvem.
> Continua precisando apenas do **.NET 8 Hosting Bundle** (que já é requisito do site).

---

## Pré-requisitos no servidor (já devem existir, do deploy manual)

- Site `Tracker` no IIS, apontando para `C:\Tracker`, com app pool próprio (ver
  [deploy-servidor-onpremises.md](deploy-servidor-onpremises.md)).
- Variáveis de ambiente de máquina com os segredos de produção (`ConnectionStrings__TrackerDb`,
  `Encryption__CpfKeyBase64`, etc.). **O CI não mexe nisso** — os segredos ficam só no servidor.

---

## O que o deploy faz no servidor (a cada execução)

1. **Backup** de `C:\Tracker` em `C:\TrackerBackups\Tracker-<data-hora>` (mantém os 5 últimos).
2. Para o **site** e o **app pool** no IIS.
3. Substitui os arquivos de `C:\Tracker` (a pasta de dados `C:\TrackerDados` fica de fora e é preservada).
4. Reinicia e faz **health check** em `https://localhost/health`. Se não responder `{"status":"ok"}`,
   o job **falha** — e o backup anterior continua disponível para rollback manual.

---

## Rollback manual

Se algo der errado após um deploy:

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

- [ ] Runner self-hosted instalado, com labels `self-hosted,windows,tracker-prod`, rodando como serviço.
- [ ] Conta do serviço com permissão de IIS + escrita em `C:\Tracker` e `C:\TrackerBackups`.
- [ ] Site `Tracker` e variáveis de ambiente de produção já configurados.
- [ ] Um deploy manual já funcionou pelo menos uma vez (valida IIS + banco + certificado).
