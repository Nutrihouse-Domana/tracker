# Instalação do agente por pendrive

Alternativa ao deploy via GPO/AD: **um único arquivo `Instalar-Agente.exe`** (≈79 MB) que a TI
copia para um pendrive e executa na máquina do colaborador. Nada precisa estar ao lado dele —
o agente publicado, o `appsettings.json` já configurado e a definição da tela de bloqueio
viajam embutidos no próprio executável.

## Uso no dia a dia

1. Espetar o pendrive na máquina do colaborador.
2. Duplo clique em **`Instalar-Agente.exe`**.
3. O Windows pede **usuário e senha** de administrador — a TI digita.
4. Aguardar a mensagem **INSTALACAO CONCLUIDA**.

A instalação cria `C:\TrackerAgent`, extrai o agente já configurado, registra o serviço
`TrackerAgent` (LocalSystem, início automático, com reinício em caso de falha e SDDL que
impede usuário comum de parar) e a tarefa `TrackerAgentLock` (tela de bloqueio). O dispositivo
aparece no Tracker em até 1 minuto, via auto-cadastro.

### Permissões das pastas (ACL)

Desde a revisão de 24/09/2026, o instalador (e o `instalar-agente.ps1`) aplica ACL **explícita,
sem herança**, por SID (funciona em Windows em português, onde "Administrators" não existe):

| Pasta | SYSTEM e Administradores | Usuários |
|---|---|---|
| `C:\TrackerAgent` (executável, `appsettings.json`, `token.dat`, log) | Controle total | Leitura e execução |
| `%ProgramData%\TrackerAgent` (`locked.flag`) | Controle total | Só leitura |

O dono das duas passa a ser Administradores e os arquivos de dentro perdem permissões próprias.
Sem isso, um usuário comum conseguia apagar o `locked.flag` ou trocar o executável. O marcador
antigo `overlay-alive.flag` é apagado: a tela de bloqueio agora é detectada pelo processo.

> **Máquinas instaladas antes desta versão não têm a ACL.** A frota Windows não tem OTA:
> **reinstale** o agente (rodar o `Instalar-Agente.exe` novo) para aplicá-la. O `token.dat` é
> preservado, então o aparelho não duplica no inventário.

Depois da instalação, editar o `appsettings.json` exige um editor aberto **como Administrador**.

O resultado fica em `logs\<HOSTNAME>-instalacao.log` ao lado do `.exe` — use para auditar o
lote de máquinas. Se a mídia estiver somente leitura, o log vai para `%ProgramData%\TrackerAgent`.

Reinstalar na mesma máquina é seguro: o `token.dat` existente é preservado, então o
dispositivo não é duplicado no backend.

Desinstalar: `Instalar-Agente.exe --desinstalar` (remove serviço, tarefa e o sinal de
bloqueio, mantendo `C:\TrackerAgent` com log e token para diagnóstico).

## Por que um `.exe` autocontido

Três restrições do ambiente da Nutrihouse moldaram o desenho — vale conhecer antes de
"simplificar" isso de volta para scripts:

1. **`.cmd`/`.bat` não conseguem pedir elevação.** Só executável com manifesto declarando
   `requireAdministrator` faz o Windows exibir a caixa de usuário e senha — é por isso que os
   instaladores comerciais conseguem e um `.bat` nunca vai conseguir. Ver
   [deploy/Instalador/app.manifest](../../deploy/Instalador/app.manifest).
2. **Execution Policy do PowerShell bloqueada por GPO.** Nenhum `.ps1` do pendrive executa, e
   `-ExecutionPolicy Bypass` é ignorado quando a política vem de diretiva de máquina. Nem a
   elevação pode depender do `powershell.exe`.
3. **Arquivo solto ao lado do executável falha em uso real.** Payload, scripts de passos e XML
   separados quebraram na mão do usuário; por isso todo o conteúdo é *embedded resource* e a
   lógica de instalação vive em C# ([deploy/Instalador/Program.cs](../../deploy/Instalador/Program.cs)),
   não em batch.

O `TrackerAgentLock.xml` é escrito em `%TEMP%` em UTF-16 LE na hora de registrar a tarefa —
`schtasks /xml` exige Unicode. E a checagem de "serviço em execução" usa o código numérico
`: 4` do `sc query`, porque o texto do estado é traduzido conforme o idioma do Windows.

### Se a elevação falhar

1. `SHIFT` + botão direito no `.exe` → *Executar como usuário diferente*.
2. Logon na máquina com a conta de administrador e executar normalmente.
3. Instalação remota da estação da TI, ou publicação por GPO/Intune (roda como SYSTEM e não
   passa por UAC).

Para descobrir qual política está barrando, rode o **`Diagnostico-UAC.cmd`** que acompanha o
kit (opcional; roda como usuário comum, não altera nada) e envie `logs\UAC-<HOSTNAME>.txt`.
O `.exe` também tem `--diagnostico`, mas — por causa do manifesto — ele só inicia elevado;
justamente o caso que não dá para investigar por ali.

## Regerar o instalador (só quando o agente ou a configuração mudarem)

Na raiz do repositório, com o `dotnet` disponível:

```powershell
.\deploy\gerar-pendrive.ps1 -Destino E:\TrackerAgent -BackendUrl https://tracker.foodsacademy.com.br
```

O script publica o agente, grava o `appsettings.json`, compacta em `payload.zip`, embute no
executável e **apaga o zip em seguida** (ele contém a `EnrollmentKey`). A pasta
`deploy/Instalador/Recursos/` é ignorada pelo git por isso mesmo.

- A `EnrollmentKey` é lida do `dotnet user-secrets` do projeto `Tracker.Api`
  (chave `Enrollment:CompanyKey`) e nunca aparece na tela. Use `-EnrollmentKey "<chave>"`
  para informar outra explicitamente.
- `-Tipo Notebook|Mobile` — tipo do dispositivo criado no auto-cadastro (padrão `Notebook`).
  `Telefone` não existe mais: celular e tablet são `Mobile`.
- `-Simular` — kit de teste: o agente não trava a tela de verdade.

A pasta de instalação (`C:\TrackerAgent`) é a constante `PastaInstalacao` em `Program.cs`.

> O executável carrega a `EnrollmentKey` embutida. O pendrive é de uso exclusivo da TI.

## Solução de problemas

| Sintoma | Causa provável |
| --- | --- |
| "compilado sem o payload do agente" | O `.exe` foi gerado por `dotnet build` direto, sem passar pelo `gerar-pendrive.ps1`. |
| `sc create` código 1073 ("o serviço já existe") | Instalação anterior cujo SDDL antigo impedia até administrador de apagar o serviço. O instalador atual resolve sozinho: reaproveita o serviço ou redefine o descritor via registro. |
| `sc create` código 5 ("acesso negado") | A conta usada no UAC não é administrador **local** desta máquina — ser admin do domínio não basta. |
| Serviço não entra em execução | Ver `C:\TrackerAgent\agent.log` — normalmente URL do backend inacessível. |
| Dispositivo não aparece no Tracker | `EnrollmentKey` divergente da configurada no servidor (`Enrollment__CompanyKey`). Veja no `agent.log`: o agente registra o status e o corpo da recusa do auto-cadastro. |
| Auto-cadastro recusado com 400 "número de série … genérico de BIOS" | A máquina não tem serial de fábrica e o instalador é anterior ao fallback `WIN-<UUID>`. Reinstale com o `Instalar-Agente.exe` atual. |
| `icacls ... falhou` na instalação | A conta não é administrador local, ou um arquivo em `C:\TrackerAgent` está aberto por outro processo. Feche e rode de novo. |
| Executável bloqueado pelo administrador | AppLocker/WDAC barrando binário em mídia removível ou não assinado — precisa de exceção por hash/caminho. |
| SmartScreen avisa "editor desconhecido" | O `.exe` não é assinado. Prosseguir em *Mais informações*, ou assinar com certificado da empresa. |

Os scripts PowerShell `deploy\instalar-agente.ps1` e `deploy\desinstalar-agente.ps1`
continuam válidos para instalação manual em máquinas sem restrição de Execution Policy.
