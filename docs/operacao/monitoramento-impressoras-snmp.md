# Monitoramento de impressoras por SNMP

As impressoras não recebem agente (não dá para instalar software nelas). Quem as acompanha é o
**próprio servidor do Tracker**: a cada 15 minutos ele consulta, por **SNMP**, as impressoras de rede
com o monitoramento ligado e grava o estado (online/offline, estado reportado, contador de páginas,
nível dos suprimentos) e um histórico de leituras. O histórico serve para conferir a **franquia de
páginas** do contrato de locação.

## O que é consultado

| Item | OID (Printer-MIB / Host-MIB) | Uso |
|---|---|---|
| Descrição do sistema | `1.3.6.1.2.1.1.1.0` (sysDescr) | diagnóstico |
| Estado do dispositivo | `1.3.6.1.2.1.25.3.2.1.5.1` (hrDeviceStatus) | "parada", "com aviso" |
| Estado da impressora | `1.3.6.1.2.1.25.3.5.1.1.1` (hrPrinterStatus) | "ociosa", "imprimindo", "aquecendo" |
| Erros detectados | `1.3.6.1.2.1.25.3.5.1.2.1` (hrPrinterDetectedErrorState) | "sem papel", "porta aberta", "papel atolado"... |
| Contador de páginas | `1.3.6.1.2.1.43.10.2.1.4.1.1` (prtMarkerLifeCount) | franquia |
| Suprimentos | tabela `1.3.6.1.2.1.43.11.1.1` (descrição `.6`, máximo `.8`, nível `.9`, colorante `.3`) | barras de toner/cilindro |
| Cor do suprimento | `1.3.6.1.2.1.43.12.1.1.4.1` (prtMarkerColorantValue) | "preto", "ciano"... |

- Versão **SNMP v2c**; sem resposta, o servidor tenta de novo em **v1** (impressoras antigas).
- Só **leitura** (GET/GETNEXT). O Tracker nunca escreve na impressora.
- Nível `-1`, `-2` ou `-3` (desconhecido / "tem algum") aparece como "nível não informado".

## Quem entra no ciclo

Só impressoras com **as três condições** (e não arquivadas):

1. "Monitorar por SNMP" ligado no cadastro;
2. conexão **Rede** (as ligadas por **USB** a um computador não são alcançáveis e nunca são consultadas);
3. **endereço de rede** preenchido (IP ou nome DNS).

## Pré-requisitos de rede

- O servidor `192.168.0.254` precisa alcançar cada impressora em **UDP 161** — inclusive as das
  **filiais**, pela VPN/link entre as unidades. Libere no firewall/roteador de cada ponta:
  origem `192.168.0.254`, destino a impressora, **UDP 161**. A resposta volta pela mesma sessão UDP.
- Prefira **IP fixo** (ou reserva no DHCP) para as impressoras. Com nome DNS, o servidor precisa
  resolver o nome.
- Se a impressora tiver lista de acesso SNMP ("hosts permitidos"), inclua `192.168.0.254`.

## Como habilitar o SNMP na impressora

O caminho muda por fabricante, mas é sempre no painel web da impressora (abra `http://<ip-da-impressora>`
no navegador, com a senha de administrador dela):

- **HP**: Rede/Networking → Segurança → *Access Control / SNMP* → habilitar **SNMPv1/v2 somente
  leitura** e definir a community de leitura.
- **Brother**: Rede → Protocolo → **SNMP** → "SNMP v1/v2c read-only access".
- **Epson / Samsung / Ricoh / Kyocera / Lexmark**: procure "SNMP" na seção de rede/protocolos.

Regras:

- Deixe **somente leitura**. Desative a community de escrita (ou troque por um valor que ninguém
  usa): com escrita, quem tiver a community pode alterar a configuração da impressora.
- A community de leitura precisa ser **igual** à configurada no servidor (`Snmp__Community`).

## Community

- O padrão de fábrica da maioria das impressoras é `public`. **Troque** por um valor próprio da
  empresa nas impressoras e no servidor — `public` é o primeiro valor que qualquer scanner de rede tenta.
- No servidor, a community vem da variável de ambiente da máquina **`Snmp__Community`**
  (valor no cofre: `<CODIGO-DO-COFRE>`). Nunca a escreva em `appsettings.json` versionado.
  Ela entra na tabela de [backup-e-chaves.md](backup-e-chaves.md).
- Uma community só para todas as impressoras. Trocar exige mudar o servidor **e** todas as
  impressoras; as que ficarem com a antiga passam a aparecer offline.

Definir no servidor (PowerShell como Administrador) e reciclar o pool do IIS:

```powershell
[Environment]::SetEnvironmentVariable("Snmp__Community", "<CODIGO-DO-COFRE>", "Machine")
Restart-WebAppPool -Name Tracker
```

## Configuração (seção `Snmp`)

Todas podem ser sobrescritas por variável de ambiente (`Snmp__<Chave>`).

| Chave | Padrão | O que faz |
|---|---|---|
| `Habilitado` | `true` | Liga o ciclo periódico. Desligado, o botão "Ler agora" continua funcionando |
| `IntervaloMinutos` | `15` | Intervalo entre ciclos |
| `Community` | `public` | Community de leitura (em produção: `Snmp__Community`) |
| `TimeoutMs` | `3000` | Timeout de **cada** requisição UDP |
| `Porta` | `161` | Porta UDP do agente SNMP |
| `MaxParalelismo` | `8` | Impressoras lidas ao mesmo tempo |
| `LimiteSuprimentoPercentual` | `15` | Suprimento com este percentual ou menos gera alerta e fica vermelho na tela |
| `FalhasParaOffline` | `2` | Falhas **seguidas** até gerar o alerta "impressora offline" |
| `RetencaoDias` | `730` | Leituras mais velhas são apagadas (poda uma vez por dia) |
| `TentarV1` | `true` | Sem resposta em v2c, tenta v1. Custa um timeout a mais por impressora offline |

Tempo de um ciclo, no pior caso: uma impressora offline leva ~2 × `TimeoutMs` (v2c + v1). Com 8 em
paralelo, 40 impressoras todas offline levam ~30 s — bem abaixo dos 15 min do intervalo.

## Alertas

Os alertas entram na mesma lista de alertas do dispositivo (tela do detalhe, botão "Tratar"):

- **Impressora offline**: após `FalhasParaOffline` leituras seguidas sem resposta.
- **Suprimento baixo**: algum suprimento em `LimiteSuprimentoPercentual`% ou menos. O detalhe diz
  qual e quanto (ex.: `Toner preto: 8%`).

Não duplica: enquanto houver um alerta **não tratado** do mesmo tipo para a impressora, outro não é
criado. Por isso, depois de trocar o toner ou religar a impressora, **trate o alerta** — senão a
próxima ocorrência não gera aviso novo.

## Páginas no período (franquia)

Na tela da impressora, "Páginas no período" mostra **maior contador − menor contador** entre as
leituras do período (datas de Brasília, as duas inclusivas; padrão: mês corrente). Precisa de pelo
menos duas leituras com contador no período. Se a impressora ficou offline boa parte do mês, o
número cobre só o intervalo entre a primeira e a última leitura bem-sucedida.

API: `GET /api/impressoras/{id}/leituras?de=2026-09-01&ate=2026-09-30`.

## Diagnóstico

**A impressora aparece offline.** Do servidor `192.168.0.254`:

1. `Test-NetConnection <ip>` — sem ping, é rede/VPN (ou a impressora está desligada).
2. SNMP responde? Com o [Net-SNMP](https://net-snmp.sourceforge.io/) ou o *SnmpWalk* da SnmpSoft:
   `snmpget -v2c -c <community> <ip> 1.3.6.1.2.1.1.1.0`. Sem resposta aqui e com ping OK, as causas
   são, em ordem: SNMP desligado na impressora, community diferente, UDP 161 bloqueado no caminho,
   impressora com lista de hosts permitidos sem o servidor.
3. Responde em v1 e não em v2c? `snmpget -v1 ...`. O Tracker já tenta v1 sozinho (`TentarV1`).

A mensagem da última falha aparece no detalhe da impressora (ex.: "Sem resposta SNMP em 3000 ms").

**Responde, mas sem suprimentos ou contador.** A impressora não implementa a Printer-MIB completa
(comum em jato de tinta doméstica). Confira com `snmpwalk -v2c -c <community> <ip> 1.3.6.1.2.1.43.11`.
Nada a fazer no Tracker.

**Log do servidor.** O ciclo registra, a cada execução, "Ciclo SNMP: N impressora(s) em X s" e um
aviso por impressora que falhou por erro inesperado. O pedido de "Ler agora" registra quem pediu.

**Ciclo não roda.** Confira `Snmp__Habilitado` (o log de start diz "Monitoramento SNMP de
impressoras desligado"). O primeiro ciclo começa ~30 s depois do start da API.
