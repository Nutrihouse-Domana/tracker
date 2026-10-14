# Atualização do agente Windows pela GPO

Os notebooks recebem o agente, e as versões novas dele, pela GPO **Deploy Tracker Agent** do
domínio `nutrihouse.intra`. A GPO é configurada **uma vez**. A cada release, o CI troca o
instalador numa pasta de rede, e cada máquina se atualiza sozinha no próximo boot.

> O Windows não tem atualização automática pelo próprio agente, como o Android (item 37 de
> [melhorias-pendentes.md](../desenvolvimento/melhorias-pendentes.md)). Este fluxo é o caminho
> para atualizar a frota sem passar de máquina em máquina.

## Como funciona

```
tag v* ──► CI (servidor .254)
             1. gera Instalar-Agente.exe + versao.txt em C:\TrackerAgentDist
             2. publicar-agente-gpo.ps1 ──► F:\AgentTracker (no próprio .254, lida pelos
                                              notebooks como \\192.168.0.254\AgentTracker)
                                              Instalar-Agente-<versao>.exe
                                              versao.txt          (gravado POR ÚLTIMO)
                                              logs\

boot do notebook ──► GPO (script de inicialização, como SYSTEM): gpo_installer.cmd
             versao.txt da rede == C:\TrackerAgent\versao.txt ?  ──► sim: sai (nada baixado)
                                                                └► não: Instalar-Agente-<versao>.exe --silencioso
```

- **`C:\TrackerAgent\versao.txt`** é gravado pelo instalador **só ao terminar com sucesso** (e
  apagado no início). Se a instalação falhar no meio, a máquina tenta de novo no boot seguinte.
- **O nome do `.exe` leva a versão.** Uma máquina pode estar rodando o instalador anterior
  direto da rede, e arquivo aberto não pode ser sobrescrito. Com um nome por versão, a cópia
  nunca briga com ninguém. O `versao.txt` só aponta para o novo depois que ele chegou inteiro
  (SHA-256 conferido). O script mantém os 2 mais recentes.
- **`--silencioso`** tira o "Pressione ENTER" do fim do instalador. Sem ele, o script da GPO
  esperava uma tecla que ninguém aperta e segurava o boot até o tempo-limite dos scripts de GPO.
- **O token é preservado:** reinstalar não duplica o aparelho no inventário.
- **Na primeira vez**, nenhuma máquina tem `versao.txt`: a frota inteira se reinstala uma vez,
  já na versão nova. Esperado.

Arquivos no repositório:

| Arquivo | Papel |
|---|---|
| [`deploy/gpo/gpo_installer.cmd`](../../deploy/gpo/gpo_installer.cmd) | Script de inicialização da GPO. Edite **só** a linha `set "PASTA=..."` |
| [`deploy/publicar-agente-gpo.ps1`](../../deploy/publicar-agente-gpo.ps1) | Leva o `.exe` e o `versao.txt` para a pasta da GPO. Idempotente |
| [`deploy/gerar-pendrive.ps1`](../../deploy/gerar-pendrive.ps1) | Gera o `.exe` e o `versao.txt` com a versão (`-Versao`; o CI passa a tag) |

## Por que não na NETLOGON

Até 14/10/2026 o instalador ficava em `\\NHNTAPP001\netlogon\agentTracker`. Isso tem dois
problemas:

1. **A NETLOGON é legível por qualquer usuário do domínio**, e o `Instalar-Agente.exe` traz o
   código de instalação (`Enrollment__CompanyKey`) num `.zip` interno: qualquer pessoa da rede
   o extrai com o 7-Zip. Com o código, dá para cadastrar aparelhos falsos e, sabendo a série de
   um aparelho real, gerar um token novo para ele.
2. **O compartilhamento NETLOGON só aceita gravação de administradores do domínio.** O CI só
   conseguiria publicar ali com uma credencial de admin do domínio, o que não se faz.

Por isso o pacote **e** o script `.cmd` vão para uma pasta própria no `.254`
(`\\192.168.0.254\AgentTracker`), com leitura só para os computadores. A GPO aponta direto para ela.

> **A GPO não transfere arquivo nenhum.** Ela só diz ao notebook "no boot, rode o arquivo do
> caminho X"; quem busca o arquivo é o **próprio notebook**, com a conta de computador dele.
> Por isso o caminho da GPO tem de ser um **compartilhamento** que o notebook alcance: unidade
> mapeada no servidor da GPO não serve (só existe lá), e `\\192.168.0.254\F$` também não
> (compartilhamento administrativo: notebook recebe acesso negado).

Depois da troca, apague o `Instalar-Agente.exe` antigo de `\\NHNTAPP001\netlogon\agentTracker`.
Como o código ficou exposto ali, a troca do `Enrollment__CompanyKey` (item 26 e item 36) também
cobre este vazamento.

## Configuração (uma vez)

### 1. Pasta compartilhada do pacote (no servidor `.254`)

A pasta fica no **próprio servidor do Tracker e do CI** (`192.168.0.254`): `F:\AgentTracker`. O
CI grava nela localmente, sem precisar de permissão de rede. Os notebooks a leem pelo
compartilhamento `\\192.168.0.254\AgentTracker`.

1. Crie `F:\AgentTracker` e, dentro, a pasta `logs`.
2. Compartilhe `F:\AgentTracker` com o nome **`AgentTracker`** (*Propriedades → Compartilhamento →
   Compartilhamento avançado*).
3. Permissões:

| Quem | Compartilhamento | NTFS em `AgentTracker` | NTFS em `AgentTracker\logs` |
|---|---|---|---|
| `Computadores do domínio` | Leitura | Leitura e execução | Modificar |
| `SYSTEM` (o runner do CI) | — (grava localmente) | Controle total | Controle total |
| `Admins. do domínio` / Administradores | Controle total | Controle total | Controle total |

> ⚠️ **Gravar nesta pasta = executar código como SYSTEM na frota inteira.** O script da GPO roda o
> `.exe` que estiver ali em todo notebook do escopo. Escrita **só** para administradores e o
> runner. `Computadores do domínio` grava apenas em `logs`, que nunca é executada.

**Tire `Usuários` e `Usuários autenticados`** do NTFS (desabilite a herança antes). É isso que
impede um colaborador de copiar o `.exe`. A pasta `logs` recebe o log de instalação de cada
máquina (`<HOSTNAME>-instalacao.log`). Sem permissão de gravação, ele vai para
`%ProgramData%\TrackerAgent` da própria máquina.

Requisitos:

- **O `.254` precisa estar no domínio `nutrihouse.intra`**: é o que permite liberar a pasta só
  para `Computadores do domínio`. Confira com `(Get-CimInstance Win32_ComputerSystem).Domain`.
- **Porta 445 (compartilhamento de arquivos) aberta só para a rede interna.** O `.254` está
  exposto à internet pela 443; a 445 nunca.

### 2. CI

Nada a configurar: o passo **Publicar agente Windows na pasta da GPO** usa `F:\AgentTracker`
por padrão quando a pasta existe. O runner roda como serviço (normalmente `SYSTEM`) e grava
localmente. Para usar outro caminho, defina a variável de **máquina** `TRACKER_AGENTE_GPO` no
`.254` e reinicie o serviço do runner (`actions.runner.*` no `services.msc`):

```powershell
[Environment]::SetEnvironmentVariable("TRACKER_AGENTE_GPO", "F:\OutraPasta", "Machine")
```

Sem a variável e sem `F:\AgentTracker`, o passo só avisa que está desligado.

### 3. Script da GPO

1. Nada a copiar: o CI coloca o [`deploy/gpo/gpo_installer.cmd`](../../deploy/gpo/gpo_installer.cmd)
   em `F:\AgentTracker` a cada deploy (desde a v1.14.1), **só quando o conteúdo mudou** e
   trocando o arquivo de uma vez. O `cmd.exe` lê o script pela rede linha a linha enquanto roda:
   reescrevê-lo no lugar, no meio de um boot, faria a máquina executar um trecho errado. Para
   mudar o script, edite-o no repositório (não na pasta: o CI sobrescreve a cópia da pasta).
2. Na GPO **Deploy Tracker Agent**: *Configuração do Computador → Políticas → Configurações do
   Windows → Scripts → **Inicialização*** → remova o script antigo e adicione
   `\\192.168.0.254\AgentTracker\gpo_installer.cmd`. Precisa ser **Inicialização** (roda como SYSTEM). Como
   script de **logon**, rodaria como o usuário, sem direito de administrador, e a instalação falharia.
3. Mantenha *Modelos Administrativos → Sistema → Logon → **Sempre aguardar a rede na
   inicialização e no logon do computador** = Habilitado*. Sem rede no boot, o script não acha a
   pasta e só tenta de novo no boot seguinte.
4. Teste de alcance: de um **notebook** (não do servidor), `Win + R` → `\\192.168.0.254\AgentTracker`. Se a pasta
   abrir, a rede e o compartilhamento estão certos.

**Ordem:** faça esta troca **depois** da primeira release que publicar o pacote na pasta (o
`versao.txt` precisa existir). Antes disso, o script sai sem fazer nada.

### 4. Testar numa OU piloto antes da frota

Vincule a GPO primeiro a uma OU com 2 ou 3 notebooks de teste:

1. `gpupdate /force` e reiniciar.
2. Conferir `C:\TrackerAgent\versao.txt` com a versão publicada e o serviço `TrackerAgent` rodando.
3. Ler `%ProgramData%\TrackerAgent\gpo-atualizacao.log` (o que o script decidiu) e
   `\\192.168.0.254\AgentTracker\logs\<HOSTNAME>-instalacao.log` (o que o instalador fez).
4. Reiniciar de novo: o log não deve ganhar linha nova de instalação (versão igual = sai na hora).
5. No painel, a coluna de versão do agente mostra a versão nova.

Só então vincule à OU da frota.

## Publicar sem o CI

Com o `.exe` e o `versao.txt` em `C:\TrackerAgentDist` (gerados pelo CI ou pelo
`gerar-pendrive.ps1`), no próprio `.254`:

```powershell
.\deploy\publicar-agente-gpo.ps1 -PastaGpo F:\AgentTracker
```

Para **voltar** para uma versão anterior, publique o `.exe` antigo: o script da GPO reinstala
sempre que a versão da rede for **diferente** da local, não só maior.

## Limitações

- **Só no boot, e só com a rede da empresa.** Notebook que só hiberna, ou que liga fora do
  escritório sem VPN antes do logon, demora ou nunca se atualiza. Para reduzir isso, crie na mesma
  GPO uma tarefa agendada diária (*Preferências → Configurações do Painel de Controle → Tarefas
  Agendadas*, conta `NT AUTHORITY\SYSTEM`) que chame o mesmo `.cmd`.
- A atualização para o agente por alguns segundos: um bloqueio emitido nesse intervalo chega pelo
  heartbeat logo depois. O `locked.flag` não é tocado, então quem está bloqueado continua.
- Instaladores gerados antes de 14/10/2026 não gravam `versao.txt` nem entendem `--silencioso`.
  Não os publique por este fluxo.

## Problemas comuns

| Sintoma | Causa provável |
|---|---|
| Resumo do CI: "publicacao na GPO desligada" | `F:\AgentTracker` não existe no `.254` (passo 1) |
| Resumo do CI: "NAO publicado na GPO" | Runner sem permissão NTFS em `F:\AgentTracker` (passo 1) |
| `gpo-atualizacao.log` com "Pasta ou versao.txt inacessivel" | Sem rede no boot (passo 3.3), `Computadores do domínio` sem leitura, porta 445 bloqueada até o `.254`, ou `.254` fora do domínio (passo 1) |
| Reinstala a cada boot | O instalador falha e não grava `versao.txt`: veja o log de instalação |
| Nada acontece e não há `gpo-atualizacao.log` | GPO não aplicada: `gpresult /r /scope computer` na máquina |
