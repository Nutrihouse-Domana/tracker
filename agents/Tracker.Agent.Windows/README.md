# Tracker Agent (Windows)

Agente de rastreabilidade instalado nos notebooks/telefones Windows da Nutrihouse.
Reporta heartbeat + localização (por IP) e executa comandos de bloqueio remoto.

## Como instalar (auto-cadastro — recomendado)

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
       "BackendBaseUrl": "http://SEU-SERVIDOR:5171",
       "AgentToken": "",
       "EnrollmentKey": "SUA-CHAVE-DA-EMPRESA",
       "Tipo": "Notebook",
       "HeartbeatIntervalSeconds": 60,
       "Simular": false
     }
   }
   ```
   > A `EnrollmentKey` fica no backend em `Enrollment:CompanyKey` (user-secrets / variável de ambiente).
   > `Simular: true` (padrão) apenas registra o bloqueio no log; defina `false` na máquina real.

3. **Instalar** como serviço (PowerShell **como Administrador**):
   ```powershell
   .\deploy\instalar-agente.ps1
   ```

O notebook aparece no inventário automaticamente (status "Em estoque", patrimônio provisório
`AUTO-<série>` que a TI ajusta depois). O token individual fica salvo em `token.dat` ao lado do
executável — execuções seguintes não recadastram.

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

## Limitação conhecida do bloqueio

O bloqueio usa `LockWorkStation` (trava a sessão). O próprio usuário ainda consegue destravar
com a senha dele do Windows. Um bloqueio que **só** a TI destrava exigiria um credential
provider customizado (fora do escopo desta fase).
