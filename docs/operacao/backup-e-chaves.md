# Backup, restauração e guarda das chaves

O que precisa de cópia de segurança no Tracker, como fazer, como testar a volta, e o que
acontece se cada chave for perdida ou vazada.

> **Resumo em uma frase:** o banco e os arquivos se recuperam de backup; a **chave do CPF** e a
> **keystore do Android** não se recuperam de jeito nenhum — só existem se alguém as guardou.

---

## O que guardar

| Item | Onde fica | Se perder | Se vazar |
|---|---|---|---|
| Banco `TrackerDb` | SQL Server do servidor `192.168.0.254` | Perde inventário, histórico, auditoria, logins | Dados pessoais expostos (CPF segue cifrado) |
| Termos de uso assinados (PDF) | `Storage__TermosDeUsoPath` — produção: `C:\TrackerDados\termos-de-uso` | Perde os termos assinados (documento com valor legal) | Dados pessoais expostos |
| APKs publicados do agente Android | `Storage__PacotesAgentePath` — ver [alerta abaixo](#alerta-apks-dentro-da-pasta-do-site) | A OTA para de funcionar até republicar | Baixo (o APK é o mesmo da frota) |
| **`Encryption__CpfKeyBase64`** | Variável de ambiente da máquina, no servidor | **CPFs ilegíveis para sempre** | CPFs do backup do banco ficam legíveis |
| `Enrollment__CompanyKey` | Variável de ambiente da máquina (servidor e runner) | Nada se perde: basta definir outra | Qualquer um cadastra aparelhos falsos no inventário |
| `ConnectionStrings__TrackerDb` | Variável de ambiente da máquina | Redefinir a senha do login `tracker_app` no SQL | Acesso direto ao banco |
| `Snmp__Community` (community SNMP das impressoras, valor `<CODIGO-DO-COFRE>`) | Variável de ambiente da máquina, no servidor, e configurada em cada impressora | Nada se perde: definir outra no servidor **e** em todas as impressoras (senão elas aparecem offline) | Leitura do status/contadores das impressoras na rede; se a community tiver escrita habilitada na impressora, alteração da configuração dela. Ver [monitoramento-impressoras-snmp.md](monitoramento-impressoras-snmp.md) |
| **Keystore do APK** (`.jks` + senhas) | Fora do repositório + secrets `ANDROID_*` do GitHub | **Não dá mais para atualizar a frota Android** | Um terceiro pode assinar APK aceito como atualização |
| Certificado HTTPS (`.pfx`) | Repositório de certificados do servidor | Site fora do ar até reemitir | Reemitir e revogar |
| Senha do ADM | Cabeça de quem usa | Recuperável (ver [Senha do ADM](#senha-do-adm-perdida)) | Trocar em **Acessos** |

As chaves e senhas vão para o **cofre de senhas da TI**. Nunca para planilha, e-mail, chat,
repositório ou documentação.

---

## Backup do banco

### Estratégia recomendada

- **Diário**, completo (`FULL`), fora do horário comercial.
- **Retenção:** 14 dias no servidor + uma cópia **fora do servidor** (compartilhamento de outra
  máquina ou mídia externa). Backup só no mesmo disco não protege contra perda do disco.
- **Antes de cada release:** o CI faz backup dos **arquivos** do site, mas **não do banco**, e as
  migrações de banco não voltam atrás. Garanta que o backup do dia já rodou antes de criar a tag.

### Qual ferramenta usar

- **SQL Server Standard:** crie um *Maintenance Plan* ou um job do SQL Agent com o `BACKUP` abaixo.
- **SQL Server Express:** não tem SQL Agent. Use o **Agendador de Tarefas do Windows** chamando o
  script abaixo.

Confira no servidor qual edição está instalada (`SELECT @@VERSION;`) antes de escolher.

### Script de backup (exemplo para o Agendador de Tarefas)

Salve como `C:\TrackerScripts\backup-tracker.ps1` (fora de `C:\Tracker`, que o deploy apaga).
Ajuste instância e pastas. **Teste manualmente uma vez antes de agendar.**

```powershell
# Backup diario do TrackerDb + termos de uso. Rodar como conta com permissao de BACKUP no SQL.
$ErrorActionPreference = "Stop"
$instancia = "localhost\SQLEXPRESS"
$destino   = "D:\TrackerBackupsBanco"          # disco diferente do sistema, se houver
$termos    = "C:\TrackerDados"
$diasReter = 14
$carimbo   = Get-Date -Format "yyyyMMdd-HHmmss"

New-Item -ItemType Directory -Force $destino | Out-Null
$arquivoBak = Join-Path $destino "TrackerDb-$carimbo.bak"

# COPY_ONLY nao quebra a cadeia de outro backup que exista; CHECKSUM detecta pagina corrompida.
$sql = "BACKUP DATABASE [TrackerDb] TO DISK = N'$arquivoBak' WITH COPY_ONLY, CHECKSUM, INIT;"
& sqlcmd -S $instancia -E -b -Q $sql
if ($LASTEXITCODE -ne 0) { throw "Falha no BACKUP (sqlcmd saiu com $LASTEXITCODE)." }

# Confere se o arquivo e restauravel (nao restaura, so le).
& sqlcmd -S $instancia -E -b -Q "RESTORE VERIFYONLY FROM DISK = N'$arquivoBak' WITH CHECKSUM;"
if ($LASTEXITCODE -ne 0) { throw "Backup gerado mas VERIFYONLY falhou: $arquivoBak" }

# Termos de uso e APKs: zip com o mesmo carimbo.
Compress-Archive -Path $termos -DestinationPath (Join-Path $destino "TrackerDados-$carimbo.zip")

# Poda: mantem so os ultimos N dias.
Get-ChildItem $destino -File |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$diasReter) } |
    Remove-Item -Force
```

- `sqlcmd` vem com o SQL Server; se não for encontrado, instale as *SQL Server Command Line
  Utilities*.
- `-E` usa a autenticação do Windows da conta que roda a tarefa. Essa conta precisa de
  permissão de backup no SQL (`db_backupoperator` no `TrackerDb`) e de escrita na pasta de destino.
- **Não escreva senha no script.** Se precisar de login SQL, use uma credencial do cofre, não texto.
- Depois do agendamento, confira **na manhã seguinte** que o `.bak` apareceu.

### Cópia para fora do servidor

Agende um `robocopy` diário de `D:\TrackerBackupsBanco` para o compartilhamento de backup da TI:

```powershell
robocopy D:\TrackerBackupsBanco \\SERVIDOR-BACKUP\Tracker /MIR /R:2 /W:10
```

`robocopy` devolve `1` quando copiou com sucesso. Só códigos `>= 8` indicam erro.

---

## Teste de restauração (fazer a cada 3 meses)

Backup que nunca foi restaurado não é backup confiável. Restaure num banco **com outro nome**, sem
tocar a produção:

```sql
-- 1) Descobrir os nomes lógicos dos arquivos dentro do .bak
RESTORE FILELISTONLY FROM DISK = N'D:\TrackerBackupsBanco\TrackerDb-AAAAMMDD-HHMMSS.bak';

-- 2) Restaurar como TrackerDb_Teste (troque os nomes lógicos pelos do passo 1)
RESTORE DATABASE [TrackerDb_Teste]
  FROM DISK = N'D:\TrackerBackupsBanco\TrackerDb-AAAAMMDD-HHMMSS.bak'
  WITH MOVE N'TrackerDb'     TO N'D:\TesteRestore\TrackerDb_Teste.mdf',
       MOVE N'TrackerDb_log' TO N'D:\TesteRestore\TrackerDb_Teste_log.ldf',
       RECOVERY, STATS = 10;

-- 3) Conferir que os dados estão lá
USE TrackerDb_Teste;
SELECT COUNT(*) AS Dispositivos FROM Dispositivos;
SELECT TOP 5 * FROM LogsAuditoria ORDER BY CriadoEm DESC;

-- 4) Apagar o banco de teste
USE master; DROP DATABASE [TrackerDb_Teste];
```

Os nomes das tabelas vêm das migrações do EF. Se `Dispositivos` ou `LogsAuditoria` não
existirem com esse nome, veja em `src/Tracker.Infrastructure/Data/Migrations`.

**O teste só está completo se o CPF abrir.** Um banco restaurado lê o CPF com a chave
`Encryption__CpfKeyBase64` **do cofre**. A forma prática de conferir é subir uma API de teste
apontando para o banco restaurado, com a chave do cofre, e abrir a tela **Usuários**. Se ela
carregar, a chave guardada é a certa. É o único jeito de descobrir **antes** do desastre que a
chave do cofre está errada.

Registre a data e o resultado de cada teste.

### Restaurar a produção de verdade

1. Pare o site no IIS (`Stop-Website -Name Tracker`).
2. `RESTORE DATABASE [TrackerDb] FROM DISK = N'...' WITH REPLACE, RECOVERY;`
3. Recoloque o login da aplicação no banco, se ele não vier junto (usuário órfão):
   ```sql
   USE TrackerDb;
   ALTER USER tracker_app WITH LOGIN = tracker_app;
   ```
4. Restaure `C:\TrackerDados` a partir do `.zip` do mesmo dia.
5. Inicie o site e confira `/health`, o login e a tela **Usuários** (prova da chave do CPF).

> Se a versão publicada for **mais nova** que o backup, a API aplica as migrações que faltam no
> start. Se for **mais velha** (backup feito depois de uma release que você desfez), volte também
> os arquivos do site com o rollback do [guia de CI/CD](deploy-automatico-cicd.md#rollback-manual).

---

## As chaves, uma a uma

### `Encryption__CpfKeyBase64`: a mais crítica

- Chave AES-256 que cifra o CPF dos detentores no banco.
- **Não existe rotação.** O código usa uma chave só (`AesStringEncryptor`). Trocar o valor da
  variável **não recifra nada**: todo CPF já gravado passa a falhar ao ser lido, e as telas que
  carregam detentores quebram. Trocar essa chave exige um desenvolvimento específico (ler com a
  antiga e regravar com a nova). **Não troque o valor em produção.**
- **Se ela vazar:** sozinha, ela não expõe nada. O risco é ela vazar **junto** com um backup do banco.
  Proteja os dois em lugares diferentes (a chave no cofre, o `.bak` no compartilhamento de backup)
  e trate como incidente de dados pessoais.
- **Se ela sumir do servidor** (reinstalação, variável apagada): a API **não sobe** (erro
  `Configuração ausente: Encryption:CpfKeyBase64`). Recoloque o valor **do cofre**. Nunca gere uma
  chave nova para "fazer subir": o site volta, mas os CPFs ficam ilegíveis de vez.
- **Como conferir que o cofre tem o valor certo, sem expor a chave:** compare um hash nos dois
  lados, nunca o valor.
  ```powershell
  $v = [Environment]::GetEnvironmentVariable("Encryption__CpfKeyBase64", "Machine")
  [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($v))).Substring(0,17)
  ```

### `Enrollment__CompanyKey`: o código de instalação dos agentes

> 🚨 **AÇÃO PENDENTE — prioridade ALTA (revisão de 24/09/2026).** O valor **real** de produção
> desta chave foi escrito no `docs/agentes/agente-android.md` e está no **histórico do Git**
> (commit `e36d642`, 13/08/2026). O arquivo atual já usa o marcador, mas qualquer clone ou cópia
> antiga do repositório recupera o valor. **Considere a chave vazada e troque-a** pelo
> procedimento "Trocar" abaixo. Backlog:
> [item 26](../desenvolvimento/melhorias-pendentes.md#26-trocar-o-código-de-instalação-vazado-no-histórico-do-git).
>
> - [ ] Valor novo gerado (20+ caracteres) e guardado **só** no cofre.
> - [ ] Variável trocada no servidor; app pool **e** serviço do runner reiniciados.
> - [ ] `C:\TrackerAgentDist\ultima-geracao.txt` apagado antes da próxima tag (senão o CI não
>       regera o instalador), ou `gerar-pendrive.ps1` rodado à mão; `.exe` antigos descartados.
> - [ ] Código novo passado aos técnicos que registram aparelhos Android.
> - [ ] Conferido: auto-cadastro com o código antigo recebe 401; aparelhos já cadastrados seguem
>       com heartbeat.
>
> **Não escreva o valor antigo nem o novo** em nenhum lugar fora do cofre ao executar a troca.

- É o "código da empresa" que autoriza um agente novo a se cadastrar sozinho. Os agentes já
  cadastrados usam token próprio e **não dependem dela** depois do cadastro.
- **Está em dois lugares:** na variável de ambiente do servidor (a API e o runner do CI) e
  **embutida no `Instalar-Agente.exe`** do agente Windows, que por isso **nunca** fica numa pasta
  que qualquer usuário lê: nem `wwwroot`, nem NETLOGON/SYSVOL (até 14/10/2026 ficou em
  `\\NHNTAPP001\netlogon\agentTracker`, legível por todo o domínio: item 36 de
  [melhorias-pendentes.md](../desenvolvimento/melhorias-pendentes.md)). No Android, o técnico digita o código
  na tela de registro.
- **Trocar (se vazar, ou por rotina):**
  1. Defina o valor novo no servidor:
     `[Environment]::SetEnvironmentVariable("Enrollment__CompanyKey", "<NOVO>", "Machine")`.
  2. Reinicie o app pool do Tracker **e** o serviço do runner do GitHub Actions (ambos só leem a
     variável ao iniciar).
  3. Regere o instalador Windows: `.\deploy\gerar-pendrive.ps1` com `-EnrollmentKey`, ou apague
     `C:\TrackerAgentDist\ultima-geracao.txt` e crie a próxima tag (o CI só regera quando o agente
     mudou desde a última geração, ou quando o marcador falta — trocar a chave não muda código).
     **Descarte os `.exe` antigos** de pendrives, de `C:\TrackerAgentDist` e da pasta da GPO
     (`\\192.168.0.254\AgentTracker`, e o antigo `\\NHNTAPP001\netlogon\agentTracker`): eles contêm
     a chave velha e vão falhar no cadastro.
  4. Passe o código novo aos técnicos que registram aparelhos Android.
- Use um valor longo (20+ caracteres). Não há motivo para ser curto: ninguém digita isso de
  memória, e no Android o técnico copia do cofre.

### `ConnectionStrings__TrackerDb`

Contém a senha do login SQL `tracker_app`. Para trocar: altere a senha no SQL
(`ALTER LOGIN tracker_app WITH PASSWORD = '...'`), atualize a variável e reinicie o app pool.

### Keystore do APK Android

Tudo sobre ela está em [agente-android.md](../agentes/agente-android.md#gerar-o-apk-de-release-distribuição-na-frota).
O essencial para a guarda:

- O `.jks` e as senhas precisam de **pelo menos duas cópias fora do repositório**, uma delas fora
  da máquina de TI (o cofre aceita anexo, ou use mídia externa guardada).
- Os secrets `ANDROID_*` do GitHub são **cópia de trabalho, não backup**. Um secret não pode ser
  lido de volta, então não serve para recuperar a chave.
- Registre no cofre **onde** ficam as cópias. O código e a documentação citam dois locais padrão
  (`Documents\TrackerKeystore\` no script `gerar-apk-agente.ps1` e `C:\NutrihouseChaves`); confirme
  qual está em uso e anote.
- **Se vazar:** não há troca sem reinstalar a frota. Restrinja quem tem acesso ao repositório e
  aos secrets, e avalie com a segurança se vale a migração.

### Senha do ADM perdida

Não precisa de backup: o próprio sistema recupera sem apagar a conta nem o histórico. No
servidor, com o pacote publicado em `C:\Tracker`:

```powershell
cd C:\Tracker
dotnet .\Tracker.Api.dll --redefinir-senha-adm --senha "NOVA_SENHA_FORTE"
```

A senha nova passa pelas **mesmas regras** da tela de Acessos (mínimo de 10 caracteres, com
maiúscula, minúscula, dígito e símbolo): se não cumprir, o comando lista os erros e não troca nada. Se a conta estiver
**desativada**, a senha é trocada, mas o comando **avisa** e **não reativa** o login — reative-o em
**Acessos** com outro ADM ou, se ele for o único, no banco (`AspNetUsers.Ativo = 1`).

Com mais de um ADM, acrescente `--email <email-do-adm>`. Em dev, o comando equivalente está no
[ambiente-dev.md](../desenvolvimento/ambiente-dev.md). Se houver outro login com a permissão **Acessos**, é mais
simples redefinir pela tela.

---

## Alerta: APKs dentro da pasta do site

Os APKs publicados na página **Agente** vão para `Storage__PacotesAgentePath`. **Sem essa
variável**, o padrão é `App_Data\pacotes-agente` **dentro da pasta do site** (`C:\Tracker`), e o
deploy **apaga `C:\Tracker` inteiro** a cada release. O banco continua anunciando a versão, mas o
arquivo sumiu, e a OTA da frota Android falha até alguém republicar o APK.

Confira no servidor e, se estiver vazio, defina a variável fora da pasta do site:

```powershell
[Environment]::GetEnvironmentVariable("Storage__PacotesAgentePath", "Machine")   # vazio = problema
[Environment]::SetEnvironmentVariable("Storage__PacotesAgentePath", "C:\TrackerDados\pacotes-agente", "Machine")
```

Depois:

- Reinicie o app pool.
- Copie os `.apk` do backup mais recente (`C:\TrackerBackups\Tracker-*\App_Data\pacotes-agente`)
  para a pasta nova. Ou republique o APK atual pela página **Agente**.

Com a pasta dentro de `C:\TrackerDados`, os APKs entram no mesmo backup dos termos.

---

## Checklist

- [ ] `Encryption__CpfKeyBase64` no cofre, com o hash conferido contra o servidor.
- [ ] `Enrollment__CompanyKey` no cofre, com 20+ caracteres.
- [ ] `Enrollment__CompanyKey` **trocado** depois do vazamento no histórico do Git (commit
      `e36d642`) — ver o [alerta](#enrollment__companykey-o-código-de-instalação-dos-agentes). Feito em: ____/____/______.
- [ ] Keystore do APK + senhas em duas cópias fora do repositório; local anotado no cofre.
- [ ] `Storage__PacotesAgentePath` definido fora de `C:\Tracker`.
- [ ] Backup diário do `TrackerDb` + `C:\TrackerDados` agendado e conferido no dia seguinte.
- [ ] Cópia para fora do servidor agendada.
- [ ] Último teste de restauração (com a tela **Usuários** abrindo): ____/____/______.
