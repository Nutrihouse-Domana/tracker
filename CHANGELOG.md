# Changelog

Mudanças de cada versão do Tracker publicadas em produção. Cada tag `v*` é um deploy (ver
[docs/operacao/deploy-automatico-cicd.md](docs/operacao/deploy-automatico-cicd.md)).

**Como ler:**

- **Banco:** a versão aplica uma migração no start. Rollback só de arquivos **não** desfaz a
  migração; é preciso restaurar o backup do banco ([docs/operacao/backup-e-chaves.md](docs/operacao/backup-e-chaves.md)).
- **Agente Windows:** o instalador (`Instalar-Agente.exe`) mudou. O CI o regera em
  `C:\TrackerAgentDist`. A frota Windows **não se atualiza sozinha**: reinstale nos notebooks
  quando a mudança importar para eles.
- **Agente Android:** saiu um APK novo (`versionCode`). Ele chega à frota por OTA **depois de
  publicado na página Agente do painel**.

**Ao criar uma tag:** acrescente a seção da versão no topo, antes do `git tag`. Use as mesmas
marcações e escreva do ponto de vista de quem usa ou opera, não do código.

---

## v1.12.0 — 01/10/2026 · Banco

Impressoras no inventário, com monitoramento opcional por SNMP.

- **Banco:** migração `Impressoras`. Acrescenta colunas novas (todas anuláveis) na tabela
  `Dispositivos` e cria a tabela `LeiturasImpressora`. Não renomeia nem move dados existentes.
  Faça o backup antes do deploy, como sempre.
- **Agente Windows / Android:** sem mudança.
- **Servidor:** novos pacotes NuGet (`Lextm.SharpSnmpLib` 12.5.7 e
  `Microsoft.Extensions.Hosting.Abstractions` 8.0.1). Defina `Snmp__Community` na máquina; sem ela, o
  servidor usa `public`. Ver [docs/operacao/monitoramento-impressoras-snmp.md](docs/operacao/monitoramento-impressoras-snmp.md).

**Impressoras**

- Tipo novo **Impressora** (Dispositivos → Novo dispositivo). Ela não tem agente e pertence sempre
  à **unidade**: sem atribuição a pessoa, termo de uso, bloqueio ou localização.
- **Alugada** é um interruptor no perfil da impressora; o prefixo `MAQ` não vale para ela. A lista e
  os relatórios seguem o interruptor.
- Conexão **Rede** ou **USB**. Só a de rede, com endereço preenchido, pode ser monitorada.
- **Monitoramento SNMP** feito pelo servidor a cada 15 min:
  - online/offline;
  - estado (sem papel, atolada...);
  - nível de toner/tinta;
  - contador de páginas;
  - **páginas no período**, para conferir a franquia da locação;
  - botão **Ler agora**.
- Alertas **impressora parou de responder** (2 falhas seguidas) e **suprimento baixo** (≤ 15%) no
  detalhe. "Tratar" pede a permissão de cadastrar dispositivos.
- A auditoria registra as alterações de aluguel, conexão e dados da impressora.

---

## v1.11.0 — 24/09/2026 · Agente Windows · Agente Android (versionCode 5)

Correções da revisão completa do código (24/09/2026).

- **Banco:** sem migração de schema. O start passa a gravar a claim
  `tracker.permissoes-configuradas` nos ADMs da role Admin (dado, não schema).
- **Agente Windows:** **exige reinstalar** o `Instalar-Agente.exe` na frota para receber as
  permissões novas das pastas (sem OTA no Windows). O CI regera o instalador uma vez no primeiro
  deploy (o marcador `ultima-geracao.txt` ainda não existe).
- **Agente Android:** 1.1.3 (`versionCode` 5). Chega à frota por OTA **depois de publicado na
  página Agente**.

**Bloqueio remoto e status**

- O "deve estar bloqueado" do heartbeat segue o **último comando** Bloquear/Desbloquear: o
  Android não destrava mais logo depois de aplicar um Bloquear ainda não confirmado.
- Aparelho **Bloqueado** só aceita a **baixa** manual (sem destravar), não abre nem conclui reparo e não pode ser
  arquivado/excluído com um Bloquear a caminho. "Bloqueado" deixou de ser opção manual.
- Desbloquear só tira de Bloqueado; volta a Em uso só com responsável (detentor ou unidade).
- Trocar a posse não deixa mais um aparelho "Em uso" sem responsável.

**Painel**

- Usuários: **Mostrar inativos**, **Reativar** e confirmação ao **Desativar**.
- Usuários e Unidades exigem também **Ver dispositivos** (antes abriam com lista vazia).
- Atribuir some para Baixado/Em manutenção/Bloqueado e recusa detentor desativado; erros de
  atribuir, alterar status, relatórios e login mostram a mensagem do servidor.
- "Excluir definitivamente" só aparece para quem tem a permissão.
- Diálogos de senha não perdem o que foi digitado quando a tela recarrega os dados.
- Login: "desativado" só é revelado com a senha certa.
- Termo de uso impresso com a data de Brasília; filtro de período da auditoria pelo dia de Brasília.
- Revogar permissões de um ADM não é mais desfeito no próximo start do site.
- **Mudança de comportamento:** `--redefinir-senha-adm` passa a exigir as regras completas de
  senha do Identity (maiúscula, minúscula, dígito, símbolo, 10+ caracteres) e avisa — sem
  reativar — quando o login está desativado.

**Agentes**

- Windows: canal em tempo real que reconecta para sempre; tela de bloqueio mais robusta (detecção
  por processo, troca de monitor com o painel Wi-Fi aberto, Gerenciador de Tarefas liberado após
  desbloquear); serial estável (`WIN-<UUID>`) em máquinas sem serial de fábrica; pastas
  protegidas por ACL.
- Android: serviço que não cai no Android 14 sem localização, bloqueio independente do token e do
  Keystore, quiosque de verdade com Device Owner, OTA e localização com prazos, Wi-Fi da tela de
  bloqueio com varredura nova.
- Auto-cadastro recusa serial genérico de BIOS sem patrimônio.

**Deploy**

- `atualizar-servidor.ps1` espera o app pool parar e a DLL ser liberada, restaura o backup se a
  troca falhar e nunca deixa o site parado.
- O instalador do agente Windows é regerado contra a **última geração bem-sucedida**, não contra a
  tag anterior.
- `gerar-apk-agente.ps1` preserva o `local.properties`; `gerar-pendrive.ps1 -Tipo` aceita `Mobile`.

**Segurança — ação pendente:** o código de instalação dos agentes (`Enrollment__CompanyKey`) de
produção está no histórico do Git e precisa ser **trocado** (ver
[backup-e-chaves.md](docs/operacao/backup-e-chaves.md#enrollment__companykey-o-código-de-instalação-dos-agentes)).

**Também nesta versão:**

- Documentação: índices revisados, manual do usuário, guia de backup e chaves, backlog de
  melhorias, protocolo do agente, privacidade/LGPD, recuperação de desastre e este changelog.
- Ambiente de desenvolvimento reproduzível: SQL Server em container e `preparar-ambiente-dev.ps1`.
- APK de release do agente Android gerado pelo CI (workflow manual **APK Agente Android**), sem a
  keystore sair do cofre de secrets.
  **Ainda não funciona:** o runner do servidor não tem o Android SDK (o preflight falha). O APK
  1.1.3 (`TrackerAgent-v1.1.3-vc5.apk`, SHA-256 `D0355FD3…D448`) foi gerado na máquina de TI com a
  keystore da frota — mesmo certificado da 1.1.2.

## v1.10.2 — 23/09/2026

- Deploy mais rápido: `node_modules` restaurado de um cache local do servidor em vez de
  reinstalado a cada release (o `npm install` consumia ~9 min dos ~12 min do deploy; a
  restauração leva ~30 s).

## v1.10.1 — 23/09/2026 · Banco

- Corrige o vencimento do aluguel, que era exibido **um dia antes**. O campo passa a ser data sem
  hora (`date`).

## v1.10.0 — 23/09/2026 · Banco

- **Vencimento do contrato de aluguel** por dispositivo, com coluna "Vence em", chip no detalhe e
  alerta de vencidos ou a vencer em 30 dias no topo da lista.

## v1.9.0 — 23/09/2026

- **Relatórios** em PDF e Excel no menu da conta: inventário, alugados x próprios e parque por
  unidade.
- Coluna **Aluguel** (Alugado/Próprio pelo prefixo `MAQ`) na lista de dispositivos.

## v1.8.0 — 22/09/2026

- Diálogo de atribuição com busca por nome/e-mail e lista paginada.

## v1.7.1 — 22/09/2026

- O cadastro de usuário mostra o erro real do servidor.

## v1.7.0 — 22/09/2026

- Listas paginadas em 10 por página, com contagem total no título.

## v1.6.1 — 22/09/2026

- Mapa de localização sempre em OpenStreetMap. O mapa escuro da CARTO passou a exigir chave de API.

## v1.6.0 — 21/09/2026

- Opção **"Colocar em uso"** para ativos do local com unidade definida.

## v1.5.0 — 17/09/2026

- **Modo claro/escuro** no site.
- Ordenação por coluna em Usuários e Unidades.

## v1.4.0 — 03/09/2026 · Banco

- **Identificação interna**: rótulo próprio da TI por dispositivo, que o agente não sobrescreve
  (pede senha do ADM).

## v1.3.2 — 02/09/2026

- Corrige 3 erros de digitação herdados do modelo oficial do termo.

## v1.3.1 — 01/09/2026

- Termo de responsabilidade no **novo modelo oficial** (versão 2.0 do termo).
- Fonte do PDF garantida em produção (Calibri Light, com Arial como reserva).

## v1.3.0 — 31/08/2026 · Agente Windows

- Tela de bloqueio lista as redes Wi-Fi de verdade (varredura pela API nativa), para o usuário
  reconectar e receber o desbloqueio.
- O agente se recadastra sozinho quando o token é rejeitado (401), em vez de ficar fora do
  inventário.
- O CI passa a regerar o instalador do agente Windows quando o agente muda.

## v1.2.3 — 25/08/2026

- Nova logo no favicon.

## v1.2.2 — 25/08/2026

- Nova identidade visual (logo) do site.

## v1.2.1 — 20/08/2026

- Lista de dispositivos com ordenação; coluna Tipo removida (as abas já separam).

## v1.2.0 — 19/08/2026 · Banco

- **Acessos por permissão**: cada login recebe permissões granulares (tela Acessos).
- **Auditoria**: trilha de quem fez o quê e quando.

## v1.1.2 — 19/08/2026 · Agente Android (versionCode 4)

- O agente Android passa a se identificar pelo **número de série da etiqueta**, digitado no
  registro, que sobrevive a reinstalação e troca de assinatura.
- Menu com o grupo Cadastros e menu da conta.

## v1.1.1 — 17/08/2026 · Agente Android (versionCode 3)

- Auto-cadastro reencontra o ativo pelo **patrimônio**, sem duplicar no inventário.
- Script para gerar o APK de release.

## v1.1.0 — 17/08/2026 · Banco · Agente Android (versionCode 2)

- **Atualização OTA** do agente Android pelo painel, sem loja.
- Identificação do aparelho por IMEI/série.

## v1.0.9 — 17/08/2026 · Banco

- **Unidades**, posse por usuário ou por local, e exclusão de dispositivo com senha (arquivar ou
  apagar de vez).

## v1.0.8 — 13/08/2026 · Banco · Agente Android (versionCode 1)

- **Agente Android** para tablets e celulares, com bloqueio remoto.
- Telefone e tablet unificados no tipo **Mobile**.

## v1.0.7 — 13/08/2026 · Agente Windows

- Instalador do agente Windows em **um único executável** (`Instalar-Agente.exe`) para pendrive.

## v1.0.6 — 05/08/2026 · Banco

- Busca no topo funcionando (dispositivos e usuários).
- Periférico sem patrimônio; status coerente na atribuição.
- Edição de usuário pela lista, com máscaras de CPF e telefone.

## v1.0.5 — 31/07/2026

- Horários corrigidos (vinham 3h adiantados).
- Status exibido com rótulo legível.

## v1.0.4 — 31/07/2026

- Sessão expirada devolve 401/403 à API em vez de redirecionar.

## v1.0.3 — 30/07/2026

- Termo: estado do dispositivo, hostname e data no documento.

## v1.0.2 — 30/07/2026

- Filtro por status e ações de status na lista de dispositivos.
- Deploy publica na pasta real do site no IIS; CI usa .NET/Node pré-instalados.

## v1.0.1 — 29/07/2026

- CI inteiro no runner self-hosted.

## v1.0.0 — 29/07/2026

- Versão inicial: inventário, histórico, termo de uso, agente Windows com bloqueio remoto e
  deploy por CI.
