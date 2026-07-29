# Teste do Agente Tracker (Windows) — Guia do Técnico

Guia rápido para instalar e testar o agente de rastreabilidade da Nutrihouse na sua máquina.
O agente roda como serviço do Windows: reporta localização/heartbeat para a plataforma e
permite que a TI **bloqueie o equipamento remotamente**.

> **Importante:** faça o teste numa máquina de trabalho comum. O agente pode travar a tela
> (bloqueio remoto). Você consegue desinstalar ao final (seção "Desinstalar").

---

## 0. Antes de começar (preencha com quem coordena o teste)

| Item | Valor |
|---|---|
| Endereço do servidor | `http://______:5171` |
| Chave da empresa (EnrollmentKey) | `______` |

- **Não precisa instalar .NET nem nada** — o pacote já vem completo (self-contained).
- Você vai receber uma pasta chamada **`TrackerAgent`**.

**Teste de conectividade primeiro:** abra no navegador `http://<endereço-do-servidor>:5171/health`.
Deve aparecer `{"status":"ok"}`. Se não abrir, avise o coordenador (provavelmente firewall/rede)
antes de continuar.

---

## 1. Copiar o agente

Copie a pasta recebida para: **`C:\TrackerAgent`**

---

## 2. Configurar

Abra `C:\TrackerAgent\appsettings.json` no Bloco de Notas e ajuste:

```json
{
  "Agente": {
    "BackendBaseUrl": "http://ENDERECO-DO-SERVIDOR:5171",
    "AgentToken": "",
    "EnrollmentKey": "CHAVE-DA-EMPRESA",
    "Tipo": "Notebook",
    "HeartbeatIntervalSeconds": 30,
    "Simular": false
  }
}
```

- `BackendBaseUrl`: o endereço do servidor (tabela do passo 0).
- `EnrollmentKey`: a chave da empresa (tabela do passo 0).
- Deixe `AgentToken` **vazio** — o agente se cadastra sozinho.
- `Simular`: deixe **`false`** para o bloqueio funcionar de verdade. (Use `true` se quiser
  testar sem que a tela trave — nesse modo o bloqueio só é registrado, não acontece.)

---

## 3. Instalar (como Administrador)

Abra o **PowerShell como Administrador**, e rode:

```powershell
cd C:\TrackerAgent
powershell -ExecutionPolicy Bypass -File .\instalar-agente.ps1
```

Isso instala o serviço `TrackerAgent` (inicia sozinho no boot) e cria a tarefa de bloqueio.

---

## 4. Verificar que registrou

- Em ~30 segundos, seu computador deve aparecer **sozinho** na plataforma, em **Dispositivos**
  (status "Em estoque", com o nome do seu computador).
- Para acompanhar o que o agente faz, abra o log: `C:\TrackerAgent\agent.log`
  (deve mostrar `heartbeat ... 200` de tempos em tempos).

---

## 5. Testar o bloqueio remoto

1. Avise o coordenador para clicar em **Bloquear** no seu dispositivo (na plataforma),
   OU faça você mesmo se tiver acesso.
2. Em segundos, sua tela deve mostrar uma **tela azul-escura de bloqueio** cobrindo tudo,
   sem botão de fechar.
3. Confirme que **não consegue sair** (Alt+Tab, tecla Windows e Gerenciador de Tarefas
   ficam bloqueados).
4. Para liberar: o coordenador clica em **Desbloquear** na plataforma. A tela some em ~1s.

---

## 6. Testar que continua bloqueado offline (importante)

Isso prova que um notebook roubado não é liberado só desligando a internet:

1. Com a máquina **bloqueada**, **desconecte a rede** (tire o cabo / desligue o Wi-Fi).
2. **Reinicie** o computador.
3. Faça login.
4. **Esperado:** a tela de bloqueio aparece automaticamente, mesmo sem internet.
5. Para liberar: reconecte a rede e peça o **Desbloquear** na plataforma.

> Na tela de bloqueio há um botão **"Conectar a uma rede Wi-Fi"** — serve para religar a
> internet sem sair da trava, para que o desbloqueio da TI chegue.

---

## 7. Desinstalar (ao final do teste)

PowerShell **como Administrador**:

```powershell
cd C:\TrackerAgent
powershell -ExecutionPolicy Bypass -File .\desinstalar-agente.ps1
```

Se a máquina estiver travada e você precisar liberar para desinstalar, peça o **Desbloquear**
pela plataforma primeiro.

---

## Problemas comuns

| Sintoma | Causa provável | Solução |
|---|---|---|
| `execução de scripts foi desabilitada` | Política do PowerShell | Use o `powershell -ExecutionPolicy Bypass -File ...` como nos passos |
| Não aparece na plataforma | Endereço errado ou servidor inacessível | Teste `http://<servidor>:5171/health` no navegador; confira o `BackendBaseUrl` |
| Serviço não inicia (erro 1053) | Config não lida / pasta errada | Rode `C:\TrackerAgent\Tracker.Agent.Windows.exe` no console para ver o erro |
| Bloqueou mas não travou a tela | `Simular: true` | Troque para `false` e reinstale |
| Quero ver o que o agente faz | — | Abra `C:\TrackerAgent\agent.log` |

Para ver o status do serviço: `Get-Service TrackerAgent`
Para reiniciar após mudar o `appsettings.json`: `sc.exe stop TrackerAgent` e depois `sc.exe start TrackerAgent`.

---

## Limitação conhecida (informe aos avaliadores)

O bloqueio impede o uso por um **usuário comum**. Quem tem acesso físico + conhecimento técnico
(modo de segurança, outro sistema, admin local) ainda pode contornar — isso é limitação do
Windows, não da ferramenta. A proteção **do dado** em caso de roubo deve ser complementada com
**BitLocker** (disco criptografado).
