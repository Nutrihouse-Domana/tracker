# Recuperação de desastre

Roteiro para pôr o Tracker de volta no ar quando o servidor de produção (`192.168.0.254`) se perdeu:
disco, reinstalação do Windows, ransomware ou troca de máquina. Para restaurar só o **banco** num
servidor que continua de pé, basta o [backup-e-chaves.md](backup-e-chaves.md#restaurar-a-produção-de-verdade).

> **Este roteiro só funciona se o [checklist do backup](backup-e-chaves.md#checklist) estiver em
> dia.** Sem backup do banco e sem a chave do CPF no cofre, ver [Cenários
> degradados](#cenários-degradados).

---

## Antes de começar: separe estes itens

| Item | Onde buscar |
|---|---|
| Último `.bak` do `TrackerDb` + zip do `C:\TrackerDados` | Cópia **fora do servidor** (compartilhamento de backup) |
| `Encryption__CpfKeyBase64` | Cofre de senhas da TI |
| `Enrollment__CompanyKey` | Cofre de senhas da TI |
| `Snmp__Community` (se as impressoras usam community própria) | Cofre de senhas da TI |
| Senha do login SQL `tracker_app` | Cofre (ou crie uma nova no passo 2) |
| Certificado HTTPS (`.pfx` + senha) ou acesso ao win-acme | Cofre / equipe de infra |
| Acesso de admin ao repositório no GitHub | Para registrar o runner de novo |
| Uma máquina com .NET 8 SDK + Node.js | Para gerar o pacote, se o runner ainda não estiver pronto |

Anote a hora do início e a data do backup usado. Tudo o que foi feito no sistema **depois** desse
backup se perdeu e precisará ser refeito (ver [Depois de voltar](#depois-de-voltar)).

---

## Ordem da recuperação

Tempo estimado: meio dia, com os itens acima em mãos. Siga a ordem: cada passo depende do anterior.

### 1. Servidor base

- Windows Server atualizado e IIS instalado. Se o servidor também hospeda o FOOD-ACADEMY, ele tem
  a própria recuperação. Não misture os dois roteiros.
- Instale o **.NET 8 Hosting Bundle** e reinicie o IIS
  ([deploy-servidor-onpremises.md, Parte 2.2](deploy-servidor-onpremises.md#22-iis--o-net-8-hosting-bundle)).

### 2. SQL Server e banco

1. Instale o SQL Server (a mesma edição de antes, ou superior) e o `sqlcmd`.
2. Crie o login da aplicação:
   ```sql
   CREATE LOGIN tracker_app WITH PASSWORD = '<SENHA-DO-COFRE>';
   ```
3. Restaure o banco com o nome **`TrackerDb`**. Os comandos (`RESTORE FILELISTONLY` + `RESTORE ...
   WITH MOVE`) estão em [backup-e-chaves.md](backup-e-chaves.md#teste-de-restauração-fazer-a-cada-3-meses).
   Troque apenas o nome de destino para `TrackerDb`.
4. Religue o usuário do banco ao login, que chega "órfão" do servidor antigo:
   ```sql
   USE TrackerDb;
   ALTER USER tracker_app WITH LOGIN = tracker_app;
   ALTER ROLE db_owner ADD MEMBER tracker_app;
   ```

### 3. Arquivos de dados

Descompacte o zip do backup em `C:\TrackerDados`. Ali ficam os termos de uso assinados e, com a
configuração recomendada, os APKs do agente Android.

### 4. Segredos (variáveis de ambiente da máquina)

Com os valores **do cofre**, nunca com valores novos. O modelo dos comandos está em
[deploy-servidor-onpremises.md, Parte 3](deploy-servidor-onpremises.md#parte-3--configurar-os-segredos-variáveis-de-ambiente-do-sistema).

- `ConnectionStrings__TrackerDb`
- `Encryption__CpfKeyBase64`: **o mesmo valor de antes**. Uma chave nova faz o site subir com todos
  os CPFs ilegíveis. Confira o hash contra o do cofre
  ([como](backup-e-chaves.md#encryption__cpfkeybase64-a-mais-crítica)).
- `Enrollment__CompanyKey`
- `Storage__TermosDeUsoPath` = `C:\TrackerDados\termos-de-uso`
- `Storage__PacotesAgentePath` = `C:\TrackerDados\pacotes-agente`
- `Snmp__Community`: a mesma configurada nas impressoras; sem ela, as que usam community própria
  aparecem **offline**.
- `Seed__*`: **dispensáveis**. O banco restaurado já tem os logins, e o seed só age quando não
  existe nenhum.

### 5. Site no IIS e HTTPS

Crie o site `Tracker` (pasta `C:\Tracker`, pool próprio "Sem Código Gerenciado", binding https 443
com host `tracker.foodsacademy.com.br` e SNI) e instale o certificado
([Partes 4 e 5](deploy-servidor-onpremises.md#parte-4--criar-o-site-no-iis-ao-lado-do-food-academy)).
Dê à identidade do pool (`IIS AppPool\Tracker`) permissão de escrita em `C:\TrackerDados`.

### 6. Publicar a aplicação

Publique **a mesma versão que rodava** (a tag mais recente, ver [CHANGELOG.md](../../CHANGELOG.md)).
Uma versão mais antiga que o banco restaurado pode não entender o schema.

- **Sem o runner ainda:** numa máquina de build, faça checkout da tag, rode
  `deploy\publicar-servidor.ps1`, copie `C:\publish\Tracker` para `C:\Tracker` e inicie o site.
- **Com o runner já registrado** (passo 8): Actions → **Deploy Producao** → *Run workflow* na tag.

### 7. Rede

- **Mesmo IP interno (`192.168.0.254`):** DNS e roteador não mudam.
- **IP interno novo:** atualize o encaminhamento da porta 443 no roteador/firewall para o IP novo.
  O registro DNS `tracker` aponta para o IP **público** e só muda se ele mudar
  ([Parte 6](deploy-servidor-onpremises.md#parte-6--dns-e-roteador)).

### 8. Runner do CI

Registre o runner self-hosted de novo, com as labels `self-hosted,windows,tracker-prod`, como
serviço e com a conta que controla o IIS
([deploy-automatico-cicd.md](deploy-automatico-cicd.md#configuração-uma-vez-instalar-o-self-hosted-runner-no-servidor)).

- Instale .NET 8 SDK e Node.js LTS e reinicie o serviço do runner.
- O primeiro deploy é lento: o cache de `node_modules` em `C:\TrackerCI` recomeça vazio.
- O runner antigo aparece como *offline* em Settings → Actions → Runners. Remova-o.

### 9. Backup de novo

Reagende o [backup diário](backup-e-chaves.md#backup-do-banco) **antes** de declarar a recuperação
concluída. O servidor novo começa sem proteção nenhuma.

---

## Verificação final

- [ ] `https://tracker.foodsacademy.com.br/health` → `{"status":"ok"}`, testado de fora da rede (4G).
- [ ] Login de um ADM funciona.
- [ ] Tela **Usuários** abre, o que prova a chave do CPF certa.
- [ ] Um termo de uso antigo abre pelo detalhe do dispositivo (prova `C:\TrackerDados`).
- [ ] Página **Agente**: a versão vigente aparece (prova os APKs).
- [ ] Um notebook e um celular voltam a mandar heartbeat (coluna **Último heartbeat** atualiza em
      até alguns minutos).
- [ ] Um bloqueio e um desbloqueio de teste num aparelho de laboratório.
- [ ] Uma impressora monitorada aparece **Online** (prova a `Snmp__Community` e a UDP 161).
- [ ] Backup diário agendado e o primeiro `.bak` conferido.
- [ ] O FOOD-ACADEMY continua no ar, se estiver no mesmo servidor.

---

## Depois de voltar

**O que aconteceu entre o backup e o desastre se perdeu.** Avise a equipe e reconstitua:

- **Aparelhos cadastrados ou recadastrados nesse intervalo:** o agente usa um token que o banco
  restaurado não conhece e recebe 401.
  - **Windows:** recadastra-se sozinho com a chave da empresa (embutida no instalador) e é
    reencontrado pela série, sem duplicar.
  - **Android:** **não** se recadastra sozinho — o app apaga o token e fica aguardando. O técnico
    precisa abrir o app e **registrar de novo** (código de instalação + série). Liste os
    celulares/tablets cujo **Último heartbeat** parou e percorra-os.
- **Bloqueios emitidos nesse intervalo:** o bloqueio fica **gravado no aparelho** e continua
  valendo, mas o banco restaurado pode achar que o aparelho está desbloqueado. Revise com a equipe
  os bloqueios recentes. Para liberar um desses aparelhos, emita **Bloquear** e depois
  **Desbloquear**: o desbloqueio só é enviado a quem o servidor conhece.
- **Atribuições, termos anexados, reparos e comentários** desse intervalo: refazer à mão, a partir
  dos papéis e do relato da equipe.
- **APK publicado nesse intervalo:** publique de novo na página **Agente**.
- **Senhas trocadas nesse intervalo:** voltam a ser as antigas. Avise os usuários, ou redefina em
  **Acessos**.

Registre o incidente e as datas (backup usado, hora da volta) em **Comentários** de um item de
controle ou no sistema de chamados da TI.

---

## Cenários degradados

| Faltou | Consequência | O que fazer |
|---|---|---|
| **Backup do banco** | Inventário, histórico, termos registrados, auditoria e logins perdidos | Suba com banco vazio (a API cria o schema e o ADM pelo `Seed__*`). Os notebooks se recadastram sozinhos; os Android precisam ser registrados de novo no app, um a um; o restante do inventário (periféricos, detentores, atribuições) é refeito à mão. Os PDFs de termos em `C:\TrackerDados` sobrevivem, mas ficam sem vínculo no sistema |
| **Chave do CPF** | CPFs do banco restaurado ilegíveis; telas de detentores quebram | Não há recuperação dos CPFs. É preciso desenvolvimento específico para limpar e recadastrar os CPFs com uma chave nova. **Não** gere uma chave nova só para o site subir sem planejar isso |
| **`C:\TrackerDados`** | Termos assinados perdidos (constam no sistema, mas o PDF não abre) | Recolher de novo as assinaturas; republicar o APK na página Agente |
| **Chave da empresa** | Nenhuma perda de dados (mas, sem ela, notebooks que precisem se recadastrar falham até a chave nova ir num instalador novo) | Defina uma nova, regere o `Instalar-Agente.exe` e avise quem registra aparelhos Android |
| **Certificado** | Site fora do ar em HTTPS | Emitir de novo (win-acme ou CA da empresa) |
| **Keystore do Android** | Não afeta a recuperação do servidor, mas impede futuras atualizações da frota | Ver [agente-android.md](../agentes/agente-android.md) e [backup-e-chaves.md](backup-e-chaves.md#keystore-do-apk-android) |

---

## Ensaio

Faça o ensaio **uma vez por ano**, ou ao trocar de servidor: numa VM isolada, siga este roteiro
até a verificação final, sem mexer na rede de produção (pule o passo 7 e teste por `hosts`). O
teste trimestral de restauração cobre só o banco; o ensaio acha os passos que faltam neste
documento.

Último ensaio: ____/____/______ — responsável: ______________________
