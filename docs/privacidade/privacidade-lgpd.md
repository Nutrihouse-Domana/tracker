# Privacidade e dados pessoais (LGPD)

Inventário **técnico** dos dados pessoais que o Tracker trata: o que é coletado, de onde vem, onde
fica, quem vê, para quem sai e por quanto tempo é guardado. Levantado do código em 23/09/2026.

> ⚠️ **Este documento descreve o que o sistema faz. Ele não é parecer jurídico.** Finalidade,
> base legal, prazos de retenção, redação do termo e comunicação aos colaboradores são decisões do
> **jurídico/DPO da Nutrihouse**. As seções marcadas **"Para o jurídico decidir"** listam o que
> precisa dessa validação. Nada aqui deve ser tratado como conformidade atestada.

---

## Titulares

| Titular | Quem é | Onde aparece |
|---|---|---|
| **Detentor** | Colaborador que recebe um equipamento | Cadastro em *Usuários*, termo de uso, histórico de movimentação |
| **Usuário do aparelho** | Quem usa o notebook/celular com agente (em geral o detentor; num ativo do local, qualquer pessoa da unidade) | Localização, dados técnicos do aparelho |
| **ADM** | Pessoa da TI com login no painel | Logins, auditoria |

---

## Inventário de dados

### Cadastro do detentor (`Usuario`)

| Dado | Proteção | Observação |
|---|---|---|
| Nome, e-mail, telefone | Nenhuma além do banco | E-mail é único |
| **CPF** | **Cifrado no banco** (AES-256-GCM) | Não pode ser alterado depois do cadastro |

### Termo de uso

- O **PDF gerado** traz nome, **CPF completo**, telefone, e-mail, estado do aparelho, número de
  série e data. O endereço impresso é o da empresa, não o do colaborador.
- O **PDF assinado** (escaneado) guarda a assinatura física.
- Fica no disco em `C:\TrackerDados\termos-de-uso\{dispositivo}\`, **sem cifragem**. A proteção
  vem das permissões da pasta e do servidor.

### Localização

Esta é a coleta mais sensível.

| | Notebook (Windows) | Celular/tablet (Android) |
|---|---|---|
| Como obtém | Geolocalização pelo **IP público** da rede em que está | **GPS/rede** pelo serviço de localização do Google (Fused Location) |
| Precisão | Cidade/região (registrada como ~5 km) | Nível de rua (a precisão real é enviada) |
| Quando | No máximo a cada **5 min** (desde a v1.14.2; antes, a cada 60 s) | A cada heartbeat (**60 s**), **inclusive em segundo plano** (permissão "Permitir o tempo todo") |
| O que grava | Latitude, longitude, "cidade, região, país" | Latitude, longitude, precisão |

- **Cada leitura grava uma linha nova**, sem comparar com a anterior: **~1.440 pontos por
  celular/tablet por dia** e **~288 por notebook** (desde a v1.14.2), sem limite de tempo.
- A tela mostra só a posição mais recente; a API devolve até as 200 últimas.
- O código prevê as origens "Wi-Fi conhecida" e "manual da TI", mas **nenhuma delas é gravada** hoje.
- Geofence existe no modelo de dados, mas **não está em uso**.

### Dados técnicos do aparelho

Hostname, sistema operacional, fabricante e modelo, **endereço MAC** (Windows), **IMEI** (Android
com Device Owner), número de série, **nível de bateria** (Android), nível de proteção do agente,
último contato.

- No Android, o hostname é o **nome do aparelho escolhido pelo usuário**, que pode conter o nome
  da pessoa (ex.: "Celular da Maria").
- O agente Windows **não coleta** o usuário logado no Windows.
- Desde 24/09/2026, em máquina **sem número de série de fábrica** o agente Windows envia no lugar
  um identificador técnico da máquina (`WIN-<UUID do SMBIOS>` ou `WIN-<MachineGuid>`). É do mesmo
  tipo do número de série (identifica o equipamento, não a pessoa) e fica no mesmo campo; vale o
  jurídico confirmar que a finalidade declarada para o número de série cobre este caso.
- A lista de redes Wi-Fi da tela de bloqueio é usada **só no aparelho**, sem ser enviada ao servidor.

### Histórico e registros

| Registro | Conteúdo pessoal |
|---|---|
| Movimentação | Detentor de origem e destino, motivo (texto livre) |
| Comentários | Texto livre da TI (pode conter qualquer coisa) |
| Comandos remotos | Quem bloqueou/desbloqueou e quando |
| **Auditoria** | Nome do ADM, ação, descrição (pode citar o nome do detentor), **endereço IP**, e-mail em tentativas de login (só quando o e-mail existe) |
| Logins ADM | Nome, e-mail, hash da senha, último login |

---

## Para quem os dados saem (terceiros)

| Terceiro | Quem envia | O que recebe | Observação |
|---|---|---|---|
| **ip-api.com** | Cada notebook, a cada heartbeat | O IP público da rede do notebook | **Por HTTP, sem criptografia**. Serviço externo sem contrato com a Nutrihouse (inferência: plano gratuito) |
| **Google** (serviços de localização do Android) | Cada celular/tablet | O tratamento de localização do próprio sistema operacional | Inerente ao Android com Google Play Services |
| **OpenStreetMap** (tile.openstreetmap.org) | O **navegador do ADM** ao abrir a aba Localização | O IP do ADM e a região do mapa exibido (revela a área aproximada da última posição) | |

Não há geocodificação reversa (endereço a partir de coordenadas) nem outro envio a terceiros. Os
**relatórios PDF/Excel não contêm CPF nem localização**, mas **contêm o nome dos detentores** (o
de termo de uso, também o de quem assinou antes). Qualquer login com "Ver dispositivos" os baixa,
e o download **não é registrado na auditoria** — é um canal de saída de nomes para fora do sistema.

---

## Quem vê o quê no painel

| Dado | Quem vê |
|---|---|
| **CPF completo do detentor** | **Qualquer login com "Ver dispositivos"**: a API de detentores devolve o CPF inteiro. A tela *Usuários* (que exige também "Gerir usuários") o mostra formatado, sem máscara |
| Histórico de localização | Qualquer login com "Ver dispositivos" |
| PDF do termo assinado | Qualquer login com "Ver dispositivos" |
| Auditoria (com IPs) | "Ver auditoria" |
| Logins ADM | "Gerir acessos" (sem CPF) |

"Ver dispositivos" é a permissão mais básica do sistema. Hoje, ver o inventário equivale a ver o
CPF e a trilha de localização de todos.

---

## Retenção e eliminação

**Não existe expurgo automático de dados pessoais.** Tudo fica guardado até alguém agir. (A única
poda automática é a das leituras SNMP das impressoras, após 730 dias, que não contêm dado pessoal.)

| Ação | Efeito nos dados pessoais |
|---|---|
| **Desativar detentor** | Só marca como inativo. **Nome, CPF, e-mail, telefone, termos e histórico permanecem.** Não há exclusão real de detentor no sistema |
| **Arquivar dispositivo** | Nada é apagado (é reversível) |
| **Excluir dispositivo definitivamente** | Apaga localizações, movimentações, comandos, reparos, comentários, alertas, os termos e os **PDFs em disco**. **A auditoria permanece** (de propósito: o registro "quem excluiu" sobrevive) |
| Desativar login ADM | Só marca como inativo |
| Backups do banco | 14 dias + cópia externa (ver [backup-e-chaves.md](../operacao/backup-e-chaves.md)). Dado apagado no sistema continua nos backups até eles vencerem |

Consequência prática: para atender um pedido de eliminação de um ex-colaborador, **hoje não há
caminho pela tela**, e o histórico de localização de um aparelho só sai junto com o aparelho.

---

## Segurança aplicada

- **Em trânsito:** HTTPS em produção (IIS). Exceções: a consulta do agente Windows ao ip-api.com
  (HTTP) e o **token do agente na URL** da conexão SignalR (pode aparecer em log de servidor/proxy).
- **Em repouso:** CPF cifrado (AES-256-GCM, chave fora do banco). Localização, contatos e PDFs **não**
  são cifrados. Proteção por acesso ao servidor e ao SQL.
- **Senhas** com hash (ASP.NET Identity), mínimo de 10 caracteres, bloqueio por tentativas.
- **Tokens dos agentes:** só o hash SHA-256 fica no banco.
  - No notebook, o token fica em `token.dat` **em texto puro** (proteção pelas permissões da pasta).
  - No Android, fica em armazenamento cifrado. Se o cifrado falhar, o app cai para o
    armazenamento comum.
- **Acesso:** permissões granulares, ações sensíveis com senha, **auditoria de toda escrita**.
- **Sem HSTS** configurado na aplicação (pode estar no IIS; confira).

---

## Transparência ao colaborador

O que o colaborador **vê** hoje:

- **Termo de uso:** o texto **não menciona** localização, rastreamento, bloqueio remoto, agente
  instalado nem tratamento de dados pessoais. O ponto mais próximo prevê "inspeções, inventários e
  verificações técnicas pela empresa".
- **Android, no registro:** explica que o app registra o aparelho no inventário e permite bloqueio
  em caso de perda ou roubo. Para a localização, diz que ela serve para registrar onde o aparelho
  esteve e pede "Permitir o tempo todo".
- **Android, notificação permanente:** "Monitorando o inventário do aparelho."
- **Windows:** o agente roda como serviço **sem aviso visível** ao usuário.
- **Tela de bloqueio** (ambos): informa que o aparelho é da Nutrihouse, foi bloqueado pela TI e
  que o suporte deve ser contatado.

---

## Para o jurídico decidir

1. **Finalidade e base legal** de cada tratamento, em especial a **localização contínua a cada
   minuto, inclusive fora do expediente**, em aparelhos que o colaborador pode levar para casa.
2. **Termo de uso:** incluir cláusula sobre agente instalado, localização, bloqueio remoto e dados
   coletados? O texto atual é o modelo oficial, e o código só muda com validação do RH/jurídico.
3. **Aviso no Windows:** o notebook não informa em lugar nenhum que há monitoramento.
4. **Prazo de retenção** para:
   - localização (hoje: para sempre);
   - dados de ex-colaboradores (hoje: para sempre);
   - auditoria e IPs;
   - termos assinados (documento com valor probatório).
5. **Eliminação a pedido do titular:** qual o procedimento? Hoje exige intervenção direta no banco.
6. **Terceiros:**
   - **ip-api.com:** manter? Contrato? Transferência internacional?
   - **Google** e **OpenStreetMap:** mesmas perguntas.
7. **Acesso ao CPF:** justifica-se que todo login com "Ver dispositivos" veja o CPF completo?
8. **Registro das operações de tratamento** e eventual relatório de impacto (RIPD), dada a
   localização.

---

## Melhorias técnicas relacionadas

As ações técnicas estão no backlog [melhorias-pendentes.md](../desenvolvimento/melhorias-pendentes.md), itens 15 a
21:

- retenção e deduplicação da localização;
- máscara e permissão própria para o CPF;
- ip-api por HTTPS ou provedor contratado;
- exclusão/anonimização de detentor;
- token fora da URL e `token.dat` protegido;
- IP real na auditoria;
- HSTS.

Várias dependem das decisões acima. Por exemplo, o prazo de retenção da localização é do jurídico,
e a rotina que o aplica é técnica.
