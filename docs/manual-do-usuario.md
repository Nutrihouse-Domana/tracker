# Manual do usuário — painel do Tracker

Guia para a equipe de TI que usa o painel em `https://tracker.foodsacademy.com.br`. Organizado
por tarefa: procure o que você quer fazer.

> **O que você vê depende das suas permissões.** Menus, abas e botões que o seu login não pode
> usar **não aparecem** (não ficam cinza — somem). Se falta algo que você precisa, peça a quem
> gerencia **Acessos**. As permissões estão na [tabela do fim](#permissões).

---

## Entrar, sair e navegar

- **Entrar:** e-mail e senha. Qualquer falha mostra **"Credenciais inválidas."** — inclusive
  login desativado ou bloqueado por muitas tentativas erradas. Se a senha está certa e mesmo
  assim não entra, peça a um gestor de Acessos para conferir se o login está ativo.
- O login fica lembrado no navegador. **Sair:** clique no seu nome (canto superior direito) →
  **Sair**.
- **Menu principal:** Dashboard, Dispositivos e **Cadastros** (Usuários, Unidades).
- **Menu da conta** (clique no seu nome): Perfil, **Relatórios**, Agente, Acessos, Auditoria.
- **Tema claro/escuro:** botão sol/lua na barra. Sem escolha, segue o tema do Windows.
- No celular, o menu vira uma gaveta (ícone ☰). **Relatórios não estão na versão celular** —
  use um computador.

### Busca global

Barra no topo: digite 2+ letras. Encontra **dispositivos** (série, patrimônio, hostname, IMEI,
nome do detentor, unidade) e **usuários** (nome, e-mail, telefone — o CPF não é pesquisável).

- ↑/↓ escolhe, Enter abre, Esc fecha.
- Enter sem escolher nada abre a lista de Dispositivos filtrada pelo termo — essa busca da lista
  é mais ampla e **também encontra a identificação interna** (ex.: `MAQ-0123`), que a barra não
  encontra.

---

## Dashboard

Cards com a contagem do parque **por tipo** (Notebooks, Mobiles, Periféricos) e **por status**
(Em uso, Em estoque, Em manutenção, Bloqueados, Baixados). Clicar num card abre a lista já
filtrada. Arquivados não entram nas contagens.

---

## Lista de dispositivos

**Abas:** Todos · Notebooks · Mobiles (celular e tablet) · Periféricos · Arquivados.
**Filtro de status** ao lado. Clique no cabeçalho de uma coluna para ordenar.

| Coluna | O que significa |
|---|---|
| Identificação | Identificação interna da TI; se vazia, o hostname; se vazio, a série |
| Aluguel | **Alugado** se a identificação começa com `MAQ`; senão **Próprio** |
| Vence em | Fim do contrato de aluguel. Chip vermelho = vencido; amarelo = vence em até 30 dias |
| Responsável | O detentor, ou o nome da unidade com o chip **Local** para ativos do local |

> **Alugado x próprio é só o prefixo `MAQ`.** Não existe campo "alugado". Para marcar um
> equipamento alugado, defina a identificação interna começando com `MAQ`. Sem identificação
> interna, vale o hostname — que o agente pode mudar sozinho. Para classificação estável, use
> sempre a identificação interna.

O **alerta amarelo/vermelho** no topo conta os aluguéis vencidos ou a vencer em 30 dias na lista
inteira (não só na página). Ordene por **Vence em** para ver quais são.

---

## Tarefas do dia a dia

### Cadastrar um dispositivo

1. **Dispositivos → Novo dispositivo** e escolha o tipo.
2. Preencha:
   - **Notebook / Mobile:** número de série, patrimônio, hostname e sistema operacional
     (obrigatórios). No Mobile, o IMEI é opcional (tablet só Wi-Fi não tem) e, se preenchido,
     precisa ter 15 dígitos.
   - **Periférico:** número de série e tipo (Mouse, Teclado, Monitor, Dock, Headset, Outro).
3. **Unidade que o dispositivo atende** e o interruptor **Pertence a um usuário / ao local**:
   - *Usuário:* o ativo terá um detentor nominal (entregue a uma pessoa, com termo).
   - *Local:* fica lotado na unidade (ex.: notebook da recepção). A unidade é obrigatória.
4. **Salvar.**

Notebooks e celulares com o agente instalado **se cadastram sozinhos** — não é preciso fazer
isto à mão para eles. Se o sistema disser que já existe um dispositivo **arquivado** com a mesma
série ou patrimônio, restaure-o (ver [Restaurar](#restaurar-um-arquivado)) em vez de cadastrar
de novo.

### Entregar um equipamento a um colaborador

1. Na **lista** de Dispositivos, botão **Atribuir** na linha do aparelho (não existe no detalhe).
2. Busque o colaborador por nome ou e-mail, marque-o e informe o **motivo** (ex.: entrega
   inicial, troca).
3. O status vira **Em uso** automaticamente.
4. Gere e colha a assinatura do **termo de uso** (próximo item).

Não é possível atribuir: ativo do local, aparelho **Baixado** (devolva ao estoque antes) ou
**Em manutenção** (conclua o reparo antes). Nesses casos a tela mostra só "Não foi possível
atribuir o dispositivo." — confira o status.

Só aparecem colaboradores **ativos** cadastrados em **Usuários**.

### Termo de uso

No detalhe do dispositivo (Notebook/Mobile), aba **Termo de uso** — exige detentor atribuído.

1. **Gerar termo para impressão:** descreva o estado do aparelho (ex.: "usado, risco na tampa")
   e clique **Gerar e baixar PDF**. Isso só gera o arquivo; nada é gravado ainda.
2. Imprima, colha a assinatura e digitalize.
3. **Anexar termo assinado:** escolha o PDF escaneado (até 20 MB) e **Enviar termo assinado**.

Os termos anexados ficam listados com o nome do detentor e a data; o ícone abre o PDF. Termo
anexado não pode ser apagado pela tela.

### Receber um equipamento de volta

No detalhe, chip de status → **Alterar status** → **Devolver ao estoque**. O detentor é
liberado e a devolução fica no histórico de movimentação.

### Enviar para conserto

Aba **Reparos** → descreva o problema (e o custo estimado, se houver) → **Abrir reparo**. O
dispositivo passa para **Em manutenção**.

- **Concluído:** o aparelho volta sozinho para **Em uso** (se tiver detentor) ou **Em estoque**.
- **Cancelado:** o status do aparelho **não muda** — ajuste-o à mão pelo chip de status.

### Dar baixa (descarte, perda definitiva)

Chip de status → **Dar baixa** → confirmar. O detentor é liberado. É reversível: **Devolver ao
estoque** traz o aparelho de volta.

### Ativo do local (sem detentor)

Aba **Unidade & Posse** → interruptor **Pertence ao local** → escolha a unidade, motivo e sua
senha. O detentor atual é liberado (fica no histórico) e o status sai de Em uso para Em estoque.
Para colocá-lo em uso na unidade: chip de status → **Colocar em uso**.

### Identificação interna e vencimento do aluguel

No detalhe, ícones de lápis:

- **Identificação interna:** o rótulo da TI (ex.: `MAQ-0123`). O agente nunca a sobrescreve.
  Pede **sua senha**. Deixar em branco volta a exibir o hostname/série. Lembre: ela decide se o
  ativo é Alugado ou Próprio.
- **Vencimento do aluguel:** data de fim do contrato. Não pede senha (fica registrado na
  auditoria). Em branco = sem contrato.

### Comentários e histórico

- **Comentários:** anotações livres da TI. Não podem ser editados nem apagados — escreva com
  cuidado.
- **Movimentação:** histórico de quem esteve com o aparelho, quando e por quê. Gerado sozinho.

---

## Bloqueio remoto

Aba **Agente & Bloqueio** (Notebook/Mobile), com permissão de bloquear.

> ⚠️ **Os botões Bloquear e Desbloquear agem na hora — sem confirmação e sem senha.** Confira
> que está no aparelho certo antes de clicar.

- **Bloquear:** o comando vai ao agente em tempo real; se o aparelho estiver offline, no próximo
  contato. O status só muda para **Bloqueado** quando o agente confirma — acompanhe em
  **Histórico de comandos** (Pendente → Enviado → Confirmado). Hoje o sistema **não marca**
  comando como falho: um comando parado em Pendente/Enviado por muito tempo significa que o
  aparelho não recebeu — confira o **Último heartbeat** e a **Proteção do agente**.
- O bloqueio é **persistente**: continua após reiniciar e sem internet. Só a TI desbloqueia.
- **Desbloquear:** o aparelho volta para Em uso (com detentor) ou Em estoque.
- Um aparelho **bloqueado não pode ser arquivado nem excluído** — desbloqueie antes.

### Aparelho perdido ou roubado

1. **Bloquear** imediatamente.
2. Aba **Localização**: última posição conhecida e horário (só a mais recente).
3. Registre o ocorrido em **Comentários** (data, quem comunicou, boletim de ocorrência se houver).
4. Recuperado: **Desbloquear** e conferir o estado. Não recuperado: mantenha bloqueado e
   **Dar baixa**; arquive só depois, se for o caso (arquivar exige desbloquear antes).

### O que mais aparece nesta aba

- **Status do agente:** último contato (heartbeat), versão, bateria, modelo.
- **Proteção do agente** (Android): *Device Owner* (máxima), *Administrador ativo* (o usuário
  consegue desinstalar) ou *Sem proteção* — **neste último o bloqueio não funciona**; reative
  pelo app no aparelho.
- **Alertas do aparelho:** avisos como "Permissão de administrador desativada" ou "Permissão de
  localização negada". Depois de resolver no aparelho, clique **Tratar**.
- **Gerar token do agente:** só para instalação manual/reconexão. O token aparece **uma única
  vez** — copie na hora. Com o instalador (`Instalar-Agente.exe`) ou o registro do app Android,
  não é necessário.

---

## Arquivar, restaurar e excluir

No detalhe ou na lista, **Excluir** abre duas opções:

| Opção | O que faz | Volta? |
|---|---|---|
| **Arquivar** (padrão) | Sai do inventário, busca, dashboard e relatórios. Histórico e termos preservados | Sim |
| **Excluir definitivamente** | Apaga o registro e tudo ligado a ele: movimentações, reparos, comentários, localizações, comandos e os **PDFs dos termos assinados** | **Não** |

Ambas pedem sua senha; a definitiva também pede digitar `EXCLUIR`. Prefira sempre **arquivar**:
o termo assinado é documento com valor legal. A exclusão definitiva exige uma permissão própria
— sem ela, o sistema recusa.

### Restaurar um arquivado

**Dispositivos → aba Arquivados → Restaurar** na linha. Age na hora, sem confirmação, e o ativo
volta com todo o histórico. Abrir o detalhe de um arquivado mostra "Dispositivo não
encontrado." — restaure primeiro. Um aparelho Android arquivado também não consegue se
registrar de novo até ser restaurado.

---

## Cadastros

### Usuários (detentores)

Pessoas que recebem equipamentos. **Não são logins do painel** — esses ficam em **Acessos**.

- **Novo usuário:** nome, CPF (validado), e-mail (único) e telefone.
- O CPF **não pode ser alterado** depois de salvo. Confira antes.
- **Desativar** age na hora, sem confirmação, e o detentor **some da lista** — hoje a tela não
  oferece como reativá-lo. Desative só quem saiu da empresa e já devolveu os equipamentos.

### Unidades

Matriz e filiais que os ativos atendem.

- **Nova unidade:** nome (único) e código opcional (ex.: `MTZ`, `FIL01`).
- O número em **Dispositivos** abre a lista filtrada por aquela unidade.
- Não é possível desativar unidade com dispositivos vinculados — remaneje-os antes.
- **Mostrar inativas** exibe as desativadas.

---

## Relatórios

Menu da conta → **Relatórios** → escolha o tipo e o formato (PDF ou Excel):

| Relatório | Conteúdo |
|---|---|
| Inventário de ativos | Todos os ativos: identificação, tipo, origem, série, patrimônio, unidade, responsável, status, data de cadastro |
| Alugados x próprios | O mesmo + vencimento e dias restantes, e o resumo alugados x próprios (critério `MAQ`) |
| Parque por unidade | Totais por unidade, alugados, próprios e distribuição por status |

- Arquivados não entram. A coluna de data é a **data de cadastro no Tracker**, não a de compra.
- O Excel traz uma aba **Resumo** e colunas de data/número de verdade (filtram e ordenam certo).

---

## Administração

### Acessos (logins do painel)

Menu da conta → **Acessos**.

- **Novo acesso:** nome, e-mail (login), senha inicial (mínimo 10 caracteres) e as permissões.
  **Administrador (tudo)** marca todas.
- **Editar**, **Redefinir senha** (derruba as sessões abertas da pessoa) e **Desativar/Ativar**
  (a pessoa perde o acesso em até 1 minuto).
- Toda ação aqui pede **a sua senha** de confirmação.
- **Proteção contra trancar a TI fora:** você não consegue tirar a sua própria permissão de
  gerir acessos, nem desativar o próprio login, nem tirar/desativar o **último** gestor de
  acessos. Peça a outro administrador.
- Mudanças de permissão valem na próxima ação da pessoa, sem precisar sair e entrar.

> Cada pessoa com seu login. Login compartilhado apaga a informação de **quem** fez cada ação
> na auditoria.

### Auditoria

Menu da conta → **Auditoria**. Registro somente-leitura de toda ação de escrita: quem, quando,
o quê, motivo e IP — inclusive logins efetuados e recusados.

- Filtre por texto (usuário ou descrição), tipo de ação e período.
- O registro sobrevive à exclusão definitiva do dispositivo.
- Relatórios baixados **não** são registrados (são só leitura).

### Agente Android (atualização da frota)

Menu da conta → **Agente**. Publica uma versão nova do app para todos os celulares e tablets.

1. Obtenha o APK de release (workflow **APK Agente Android** no GitHub — ver
   [agente-android.md](agente-android.md)).
2. **Publicar nova versão** → escolha o `.apk` → informe `versionCode` e `versionName` **iguais**
   aos do build (o resumo do workflow mostra) → sua senha → **Publicar**.
3. Os cards mostram quantos aparelhos já estão na versão e quantos faltam; os desatualizados se
   atualizam no próximo contato com o servidor.

O `versionCode` precisa ser **maior** que o vigente — o Android recusa versão igual ou menor.
**Publicar afeta a frota inteira**: publique só APK testado num aparelho de laboratório.

O código de instalação usado no registro dos aparelhos **não aparece no painel** — está no cofre
de senhas da TI (ver [backup-e-chaves.md](backup-e-chaves.md)).

### Perfil

Mostra seu nome, e-mail e as permissões do seu login. Não há troca de senha pela própria
pessoa: peça a um gestor de Acessos para **Redefinir senha**.

---

## Permissões

| Permissão | Libera |
|---|---|
| Ver dispositivos | Dashboard, busca, lista, detalhe e relatórios |
| Cadastrar/editar | Criar dispositivos, unidade atendida, identificação interna, vencimento do aluguel, gerar token do agente |
| Movimentar | Atribuir, status, posse, reparos, comentários, termos de uso |
| Arquivar | Arquivar e restaurar |
| Excluir definitivo | Apagar de vez (irreversível) |
| Bloquear/desbloquear | Bloqueio remoto e tratar alertas do aparelho |
| Publicar APK do agente | Página Agente |
| Gerir usuários (detentores) | Cadastros → Usuários |
| Gerir unidades | Cadastros → Unidades |
| Ver auditoria | Página Auditoria |
| Gerir acessos | Página Acessos (logins e permissões) |

---

## Limitações conhecidas

Comportamentos atuais do sistema que vale conhecer. Detalhes técnicos e correção sugerida de
cada um: [melhorias-pendentes.md](melhorias-pendentes.md).

- A tela de login não distingue senha errada de login desativado/bloqueado.
- **Bloquear, Desbloquear, Restaurar** e **desativar detentor** não pedem confirmação.
- Detentor desativado não pode ser reativado pela tela.
- **Atribuir** e **Alterar status** não mostram o motivo quando o sistema recusa.
- A opção **Excluir definitivamente** aparece para quem só pode arquivar; o sistema recusa ao
  confirmar.
- Alguns rótulos aparecem em forma técnica: status do reparo (`EmAndamento`, `Concluido`),
  comandos (`Bloquear`, `Pendente`), origem da localização (`GpsAndroid`, `IpGeolocationWindows`)
  e, na auditoria, a ação de vencimento de aluguel.
- A busca global não encontra pela identificação interna (use Enter para buscar na lista).
- Relatórios não estão disponíveis no menu do celular.
