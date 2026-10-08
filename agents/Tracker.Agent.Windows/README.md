# Tracker Agent (Windows)

Agente de rastreabilidade instalado nos notebooks Windows da Nutrihouse.
Reporta heartbeat + localização (por IP) e executa comandos de bloqueio remoto.

## Como instalar (recomendado: `Instalar-Agente.exe`)

O caminho normal é o instalador autocontido, que já leva o endereço de produção e a chave da
empresa embutidos — duplo clique no notebook, credencial de administrador e pronto. O CI o regera
em `C:\TrackerAgentDist` quando o agente muda. Passo a passo:
[docs/agentes/instalacao-agente-pendrive.md](../../docs/agentes/instalacao-agente-pendrive.md).

As seções abaixo são a referência técnica (instalação manual e desenvolvimento).

## Instalação manual (auto-cadastro)

Com o auto-cadastro, você **não precisa cadastrar o notebook à mão** nem gerar token por
aparelho: basta usar a **chave da empresa** (a mesma em todas as máquinas). Na primeira
execução o agente se registra sozinho no inventário e guarda seu token individual.

1. **Publicar** o agente (na máquina de build, com .NET 8 SDK). Use **self-contained** para o
   .NET já vir embutido — assim a máquina de destino **não precisa ter nada instalado**:
   ```bash
   dotnet publish agents/Tracker.Agent.Windows -c Release -r win-x64 --self-contained true -o C:\TrackerAgent
   ```
   > (Alternativa "leve": `--self-contained false` gera ~5 MB, mas exige o **.NET 8 Runtime**
   > instalado em cada máquina de destino.)

2. **Configurar** `C:\TrackerAgent\appsettings.json` — deixe `AgentToken` vazio e informe a chave:
   ```json
   {
     "Agente": {
       "BackendBaseUrl": "https://tracker.foodsacademy.com.br",
       "AgentToken": "",
       "EnrollmentKey": "<CODIGO-DO-COFRE>",
       "Tipo": "Notebook",
       "HeartbeatIntervalSeconds": 60,
       "Simular": false
     }
   }
   ```
   > A `EnrollmentKey` é a mesma do backend em `Enrollment:CompanyKey` (variável de ambiente em
   > produção, user-secrets em dev). Para testar contra a máquina de dev, use
   > `http://<ip-da-maquina>:5171` e a chave do user-secrets dela.
   > `Simular: true` apenas registra o bloqueio no log. **Atenção:** o default no código é `true`;
   > se a chave sumir do arquivo, o agente para de bloquear sem avisar — mantenha `false` explícito
   > na máquina real.

3. **Instalar** como serviço (PowerShell **como Administrador**):
   ```powershell
   .\deploy\instalar-agente.ps1
   ```

O notebook aparece no inventário automaticamente (status "Em estoque", patrimônio provisório
`AUTO-<série>` que a TI ajusta depois). O token individual fica salvo em `token.dat` ao lado do
executável — execuções seguintes não recadastram. Se o servidor recusar o token (401), o agente
apaga o `token.dat` e se recadastra sozinho.

### Alternativa: cadastro manual (token por aparelho)

Se preferir controle total, deixe `EnrollmentKey` vazio e gere o token na plataforma:
dispositivo → aba **Agente & Bloqueio** → **Gerar token do agente** → cole em `AgentToken`.

O serviço sobe no boot, roda como `LocalSystem` e fica protegido contra parada/remoção por
usuários comuns (um administrador local do Windows ainda consegue removê-lo).

## Testar sem instalar (modo console)

```bash
dotnet run --project agents/Tracker.Agent.Windows
```

Com `Simular: true`, o comando de bloqueio é apenas logado. Útil para validar heartbeat e
recebimento de comandos sem travar a tela.

## Como o bloqueio funciona

- O serviço roda na sessão 0 e não consegue desenhar na tela do usuário. O bloqueio é uma
  **tela cheia** (`LockOverlay`, uma janela por monitor) lançada na sessão do usuário pela
  **tarefa agendada** `TrackerAgentLock`, criada pelo instalador.
- É **persistente**: o sinal fica em `%ProgramData%\TrackerAgent\locked.flag`. O agente re-trava
  sozinho (a cada 2 s) mesmo offline ou depois de reiniciar; só o **Desbloquear** da TI remove o
  sinal.
- A tela de bloqueio permite escolher uma rede Wi-Fi, para o notebook conseguir receber o
  Desbloquear.

**Limitações:** um administrador local do Windows (ou o modo de segurança) consegue contornar a
trava; e o agente ainda não reaplica o bloqueio pelo `deveEstarBloqueado` do heartbeat se o
`locked.flag` sumir (item 22 de
[melhorias-pendentes.md](../../docs/desenvolvimento/melhorias-pendentes.md)).
