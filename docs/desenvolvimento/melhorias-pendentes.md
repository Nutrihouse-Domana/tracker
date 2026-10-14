# Melhorias pendentes

Backlog de limitações conhecidas do Tracker, levantado em 23/09/2026 na revisão da documentação,
atualizado em 24/09/2026 na revisão completa do código (itens 26 a 32) e conferido de novo
contra o código em 08/10/2026 (itens 34 e 35 novos).
Cada item diz o problema, onde está no código, a correção sugerida e como conferir.

**Ao corrigir um item:**

1. Marque-o como resolvido aqui, com data e PR.
2. Remova-o da seção "Limitações conhecidas" do [manual-do-usuario.md](../uso/manual-do-usuario.md) e
   atualize o trecho do manual que descreve a tela.
3. Registre no `CLAUDE.md` a pegadinha que tiver aparecido no caminho.

As regras do projeto continuam valendo em toda correção:

- confirmação de senha errada devolve **400, nunca 401**, porque o axios desloga em 401;
- a permissão é checada **nos dois lados** (policy no backend + ocultar no front);
- toda escrita chama `IAuditoria.Registrar`;
- valor novo de enum entra **no fim**.

## Resumo

| # | Item | Prioridade | Esforço |
|---|---|---|---|
| 1 | [Bloquear/Desbloquear sem confirmação](#1-bloquear-e-desbloquear-sem-confirmação) | **Alta** | Pequeno |
| 2 | [Restaurar e desativar detentor sem confirmação](#2-restaurar-e-desativar-detentor-sem-confirmação) — **Parcial** (desativar ok; falta Restaurar) | Média | Pequeno |
| 3 | [Detentor desativado não pode ser reativado](#3-detentor-desativado-não-pode-ser-reativado) — ✅ Resolvido em 24/09/2026 | **Alta** | Pequeno |
| 4 | ["Excluir definitivamente" visível sem a permissão](#4-excluir-definitivamente-visível-sem-a-permissão) — ✅ Resolvido em 24/09/2026 | Média | Pequeno |
| 5 | [Atribuir e Alterar status escondem o motivo do erro](#5-atribuir-e-alterar-status-escondem-o-motivo-do-erro) — ✅ Resolvido em 24/09/2026 | Média | Pequeno |
| 6 | [Login não distingue desativado/bloqueado](#6-login-não-distingue-desativado-ou-bloqueado) — **Parcial** (desativado ok; falta lockout) | Baixa | Pequeno |
| 7 | [Rótulos técnicos sem tradução](#7-rótulos-técnicos-sem-tradução) — **Parcial** (auditoria ok; faltam reparo, comandos e origem) | Baixa | Pequeno |
| 8 | [Busca global ignora a identificação interna](#8-busca-global-ignora-a-identificação-interna) — ✅ Resolvido em 13/10/2026 | Média | Pequeno |
| 9 | [Relatórios ausentes no menu do celular](#9-relatórios-ausentes-no-menu-do-celular) | Baixa | Pequeno/Médio |
| 10 | [Detalhe de arquivado mostra "não encontrado"](#10-detalhe-de-arquivado-mostra-não-encontrado) | Baixa | Médio |
| 11 | [Status de comando "Falhou" nunca é gravado](#11-status-de-comando-falhou-nunca-é-gravado) | Média | Médio |
| 12 | [Cancelar reparo deixa o aparelho em manutenção](#12-cancelar-reparo-deixa-o-aparelho-em-manutenção) | Baixa | Pequeno |
| 13 | [Chave do CPF sem rotação](#13-chave-do-cpf-sem-rotação) | Média | Grande |
| 14 | [Comentários desatualizados sobre relatórios no Perfil](#14-comentários-desatualizados-sobre-relatórios-no-perfil) — **Parcial** (faltam `App.tsx` e `AppShell.tsx`) | Baixa | Trivial |
| 15 | [Localização gravada a cada minuto, para sempre](#15-localização-gravada-a-cada-minuto-para-sempre) | **Alta** | Médio |
| 16 | [CPF completo para quem só vê o inventário](#16-cpf-completo-para-quem-só-vê-o-inventário) | **Alta** | Pequeno |
| 17 | [Geolocalização do Windows por HTTP em serviço gratuito](#17-geolocalização-do-windows-por-http-em-serviço-gratuito) | Média | Pequeno |
| 18 | [Sem exclusão/anonimização de detentor](#18-sem-exclusãoanonimização-de-detentor) | Média | Médio |
| 19 | [Token do agente na URL e em texto puro](#19-token-do-agente-na-url-e-em-texto-puro) | Média | Médio |
| 20 | [Conferir o IP gravado na auditoria](#20-conferir-o-ip-gravado-na-auditoria) | Baixa | Pequeno |
| 21 | [HSTS não configurado na aplicação](#21-hsts-não-configurado-na-aplicação) | Baixa | Trivial |
| 22 | [Agente Windows ignora "deve estar bloqueado"](#22-agente-windows-ignora-deve-estar-bloqueado) — ✅ Resolvido em 13/10/2026 | Média | Pequeno |
| 23 | [Agente Android não se recadastra sozinho](#23-agente-android-não-se-recadastra-sozinho) | Média | Médio |
| 24 | [Agente Windows informa sempre a versão 1.0.0](#24-agente-windows-informa-sempre-a-versão-100) — ✅ Resolvido em 13/10/2026 | Média | Trivial |
| 25 | [Inconsistências menores nos agentes](#25-inconsistências-menores-nos-agentes) — **Parcial** (comentários "Telefone" e "2 min" ok) | Baixa | Trivial |
| 26 | [Trocar o código de instalação vazado no histórico do Git](#26-trocar-o-código-de-instalação-vazado-no-histórico-do-git) | **Alta** | Pequeno |
| 27 | [Android bloqueado com token revogado fica preso](#27-android-bloqueado-com-token-revogado-fica-preso) | Média | Médio |
| 28 | [Máquinas Windows com serial genérico duplicam ao recadastrar](#28-máquinas-windows-com-serial-genérico-duplicam-ao-recadastrar) | Baixa | Pequeno |
| 29 | [Relatórios convertem a data pelo fuso do servidor](#29-relatórios-convertem-a-data-pelo-fuso-do-servidor) — **Parcial** (relatório de termos ok) | Baixa | Trivial |
| 30 | [Heartbeat sem a data da leitura da localização](#30-heartbeat-sem-a-data-da-leitura-da-localização) | Baixa | Médio |
| 31 | [DisableTaskMgr removido em todo logon](#31-disabletaskmgr-removido-em-todo-logon) | Baixa | Pequeno |
| 32 | [Atribuir aparelho Bloqueado: só o site recusa](#32-atribuir-aparelho-bloqueado-só-o-site-recusa) | Baixa | Trivial |
| 33 | [Aparelho perdido fica Bloqueado para sempre](#33-aparelho-perdido-fica-bloqueado-para-sempre) — ✅ Resolvido em 24/09/2026 | Média | Pequeno |
| 34 | [Agente pode postar alerta de impressora](#34-agente-pode-postar-alerta-de-impressora) | Baixa | Trivial |
| 35 | [`@microsoft/signalr` instalado no site sem uso](#35-microsoftsignalr-instalado-no-site-sem-uso) | Baixa | Trivial |
| 36 | [Instalador do agente na NETLOGON expõe o código de instalação](#36-instalador-do-agente-na-netlogon-expõe-o-código-de-instalação) | **Alta** | Pequeno |
| 37 | [Agente Windows sem atualização automática própria](#37-agente-windows-sem-atualização-automática-própria) | Média | Grande |

**Sugestão de ordem:**

0. Itens **26 e 36** antes de tudo: são segurança e não dependem de código (pasta nova da GPO + troca da chave).
1. Item **1**: é o que mais afeta o dia a dia (o 3 já foi resolvido).
2. O que sobrou do item **2** (confirmação no Restaurar) e o **6** (mensagem de lockout).
3. Item **11** (8, 22 e 24 resolvidos em 13/10/2026) — antes, decida a política: ver o risco descrito nele.
4. Itens **15 a 19** (privacidade): vários dependem de decisão do jurídico/DPO — ver
   [privacidade-lgpd.md](../privacidade/privacidade-lgpd.md#para-o-jurídico-decidir). O **16** não depende e pode
   ir já.
5. Os demais, conforme houver tempo.

---

## 1. Bloquear e Desbloquear sem confirmação

**Problema:** os botões da aba *Agente & Bloqueio* emitem o comando no primeiro clique, sem
diálogo e sem senha.

**Impacto:** um clique errado trava o aparelho de um colaborador. O bloqueio é persistente e
continua mesmo offline. É a ação de maior efeito do sistema e a única sem nenhuma barreira: arquivar,
mudar identificação e trocar posse, bem menos graves, pedem senha.

**Onde:**

- `src/Tracker.Web/src/features/dispositivos/AgenteSection.tsx`: `onClick={() => emitir.mutate('Bloquear')}`
  e o equivalente para `'Desbloquear'`.
- `src/Tracker.Api/Controllers/DispositivosAgenteController.cs`: endpoint que emite o comando.

**Correção sugerida:**

- **Front:** diálogo de confirmação que mostra a identificação exibida (`identificacaoExibida`) e o
  detentor, com campo **Motivo** (o que vai para a auditoria) e **Sua senha do Tracker**. Mesmo
  padrão de `EditarIdentificacaoInternaDialog.tsx`.
- **Backend:** receber a senha no request e validar com `ConfirmacaoSenhaAdm` (400 em senha
  errada). Gravar o motivo no `Registrar(...)` da auditoria.
- **Decisão a tomar:** pedir senha também para **Desbloquear**? Desbloquear por engano é menos
  grave, mas libera um aparelho que pode estar roubado. Recomendação: pedir nos dois.

**Verificar:** teste de integração que emite o comando com senha errada (espera 400, sem
comando criado) e com senha certa (comando `Pendente` + registro na auditoria).

---

## 2. Restaurar e desativar detentor sem confirmação

> **Parcial — 24/09/2026 (revisão completa do código).** **Resolvido:** *Desativar* detentor
> abre o diálogo "Desativar usuário" antes de agir (`UsuariosPage.tsx`), e o detentor pode ser
> reativado (item 3). **Pendente:** *Restaurar* na aba *Arquivados* continua agindo no clique.

**Problema:** **Restaurar** (aba *Arquivados*) e **Desativar** detentor (tela *Usuários*) agem no
clique.

**Impacto:** menor que o item 1, já que as duas ações são reversíveis. Ainda assim, desativar
detentor hoje **não tem volta pela tela** (item 3), e isso torna o clique errado caro.

**Onde:**

- `src/Tracker.Web/src/pages/DispositivosPage.tsx`: botão "Restaurar" da aba Arquivados.
- `src/Tracker.Web/src/pages/UsuariosPage.tsx`: ícone "Desativar".

**Correção sugerida:** diálogo simples de confirmação ("Restaurar {identificação}?" /
"Desativar {nome}? Ele deixa de aparecer para atribuição."). Senha não é necessária: nenhuma das
duas muda responsabilidade sobre ativo.

**Verificar:** manual, no navegador. Cancelar no diálogo não pode disparar a requisição.

---

## 3. Detentor desativado não pode ser reativado

> ✅ **Resolvido em 24/09/2026 (revisão completa do código).** **Resolvido:** a tela carrega
> também os inativos e tem o interruptor **"Mostrar inativos"** (desligado por padrão), com chip
> *Inativo* e botão **Reativar**. Reativar usa o `PUT /api/usuarios/{id}` com `ativo = true` e
> fica auditado como edição do detentor (não foi criada ação de auditoria própria). A paginação
> volta à página 1 ao mudar a busca ou o filtro. O `Atribuir` do backend passou a recusar
> detentor inativo (400).

**Problema:** a tela *Usuários* só pede os ativos (`useUsuarios()` com `incluirInativos = false`).
O detentor desativado some, e com ele o botão **Editar**, que é onde fica o interruptor *Ativo*.
O chip "Inativo" do código nunca chega a aparecer.

**Impacto:** quem desativou por engano, ou quem voltou para a empresa, não pode ser reativado.
O único caminho é mexer direto no banco. Recadastrar também não funciona, porque o e-mail é único.

**Onde:**

- `src/Tracker.Web/src/features/usuarios/hooks.ts`: `useUsuarios(incluirInativos = false)`.
- `src/Tracker.Web/src/pages/UsuariosPage.tsx`: chama sem parâmetro.
- O backend já suporta: `GET /api/usuarios?incluirInativos=true`.

**Correção sugerida:** interruptor **"Mostrar inativos"** na tela, igual ao da `UnidadesPage.tsx`,
passando `incluirInativos` ao hook. O formulário de edição já tem o interruptor Ativo/Inativo.
Conferir se o backend audita a reativação (hoje há `DetentorDesativado`; talvez falte a ação de
reativação, que entraria **no fim** do enum).

**Verificar:** desativar → ligar "Mostrar inativos" → editar → ativar → conferir que o detentor
volta a aparecer no diálogo **Atribuir** e que a auditoria registrou.

---

## 4. "Excluir definitivamente" visível sem a permissão

> ✅ **Resolvido em 24/09/2026 (revisão completa do código).** **Resolvido:** sem
> `dispositivos.excluir_definitivo` o diálogo (`ExcluirDispositivoDialog.tsx`) mostra só
> *Arquivar*. O texto do diálogo usa `identificacaoExibida`.

**Problema:** o diálogo de exclusão mostra a opção **Excluir definitivamente** para quem tem só
`dispositivos.excluir`. O backend recusa com 403 ao confirmar, depois de o usuário ter digitado
`EXCLUIR` e a senha, e a mensagem que aparece é genérica.

**Impacto:** a regra do projeto é "ocultar, não desabilitar". Aqui a opção não é nem ocultada, e
o erro chega tarde e sem explicação.

**Onde:**

- `src/Tracker.Web/src/features/dispositivos/ExcluirDispositivoDialog.tsx` (modos `'arquivar'` /
  `'definitiva'`).
- Na aba *Arquivados*, o ícone já é gateado corretamente. Só o diálogo não é.

**Correção sugerida:** esconder a opção com `usePermissao(PERMISSOES.dispositivosExcluirDefinitivo)`.
Sem ela, o diálogo mostra só *Arquivar*, sem a escolha. Caso de borda: para um dispositivo **já
arquivado**, sem essa permissão não sobra ação nenhuma, e o diálogo nem deveria abrir.

**Verificar:** login com só `dispositivos.excluir` não vê a opção; login com as duas vê as duas.

---

## 5. Atribuir e Alterar status escondem o motivo do erro

> ✅ **Resolvido em 24/09/2026 (revisão completa do código).** **Resolvido:** os dois mostram a
> mensagem do servidor via `mensagemErroApi`. Além disso, **Atribuir** nem aparece para
> aparelho *Baixado*, *Em manutenção* ou *Bloqueado* (`statusPermiteAtribuir` em
> `features/dispositivos/status.ts`), e o menu de status de um *Bloqueado* só oferece
> **Dar baixa** (item 33), com a dica "Desbloqueie pelo comando remoto (só a baixa é manual).". Ver também o item 32.

**Problema:**

- **Atribuir:** qualquer recusa vira "Não foi possível atribuir o dispositivo.".
- **Alterar status:** o componente não tem bloco de erro nenhum. A recusa passa **em silêncio**:
  o menu fecha e nada muda.

Em ambos os casos o backend devolve mensagens claras, como "Conclua o reparo antes de atribuir"
ou "Devolva-o ao estoque antes de atribuir".

**Impacto:** o usuário não sabe por que não funcionou e acha que o sistema travou.

**Onde:**

- `src/Tracker.Web/src/features/dispositivos/AtribuirDialog.tsx`: mensagem fixa.
- `src/Tracker.Web/src/features/dispositivos/StatusDispositivoAcoes.tsx`: sem tratamento de erro.
- Helper já existente: `src/Tracker.Web/src/utils/erroApi.ts`.

**Correção sugerida:** usar o helper de `erroApi.ts` nos dois (como o `DispositivoFormDialog`
já faz). No status, exibir um `Alert` ou snackbar com a mensagem do servidor.

**Verificar:** tentar atribuir um aparelho *Em manutenção* e colocar em uso um ativo de local sem
unidade. A mensagem do backend precisa aparecer.

---

## 6. Login não distingue desativado ou bloqueado

> **Parcial — 24/09/2026 (revisão completa do código).** **Resolvido:** a tela mostra a
> mensagem do servidor (fallback "Credenciais inválidas."), e o `AuthController` confere a senha
> **antes** (`CheckPasswordSignInAsync`, com lockout): "Este login está desativado…" só aparece
> com a senha certa, então a tela não revela quais logins existem. **Pendente:** a mensagem
> específica de bloqueio por tentativas (`IsLockedOut`) — hoje o lockout ainda sai como
> "Credenciais inválidas.".

**Problema:** a tela mostra sempre "Credenciais inválidas.". O backend já devolve "Este login está
desativado. Fale com um administrador." para login desativado.

**Impacto:** quem teve o login desativado, ou bloqueado por excesso de tentativas, acha que errou
a senha, tenta de novo e agrava o bloqueio.

**Onde:** `src/Tracker.Web/src/pages/LoginPage.tsx` (texto fixo) e
`src/Tracker.Api/Controllers/AuthController.cs`.

**Correção sugerida:** exibir a mensagem do backend quando houver. Para o bloqueio por
tentativas, o `SignInResult.IsLockedOut` permite devolver "Muitas tentativas. Tente novamente em
alguns minutos.".

> ⚠️ **Não revele se o e-mail existe.** Senha errada e e-mail inexistente devem continuar com a
> **mesma** mensagem genérica, senão a tela vira ferramenta para descobrir quais logins existem.
> Só diferencie *depois* que a senha foi validada (desativado) ou o lockout se aplicou.

**Verificar:** e-mail inexistente e senha errada dão a mesma mensagem. Login desativado com senha
certa dá a mensagem específica.

---

## 7. Rótulos técnicos sem tradução

> **Parcial — 08/10/2026 (conferência da documentação).** **Resolvido:** a auditoria lê os rótulos
> das ações da API (`AuditoriaController`), inclusive o do vencimento de aluguel. **Pendente:**
> reparo, comandos e origem da localização.

**Problema:** alguns valores de enum aparecem crus na tela.

| Onde aparece | Valores |
|---|---|
| Status do reparo (`ReparosSection.tsx`) | `EmAndamento`, `Concluido` |
| Histórico de comandos (`AgenteSection.tsx`) | `Bloquear`, `Desbloquear`, `Pendente`, `Enviado`, `Confirmado` |
| Origem da localização (`LocalizacaoSection.tsx`) | `GpsAndroid`, `IpGeolocationWindows`, `WifiConhecida`, `ManualTI` |

**Correção sugerida:** mapas de rótulo, como os que já existem para o status do dispositivo. Vale criar um teste ou uma
checagem de tipo que falhe quando um valor novo de enum ficar sem rótulo, porque foi exatamente
assim que a ação do vencimento escapou da auditoria.

---

## 8. Busca global ignora a identificação interna

> ✅ **Resolvido em 13/10/2026 (PR #48).** `BuscaController`
> procura também em `IdentificacaoInterna` (na base, então vale para todos os tipos, impressora
> inclusive) e devolve o campo no `BuscaDispositivoDto`; a `BarraDeBusca` exibe
> `identificacaoExibida` e repete o hostname no texto secundário. Coberto por
> `BuscaTests.Busca_PorIdentificacaoInterna_EncontraEDevolveORotulo`.

**Problema:** a barra de busca do topo procura por série, patrimônio, detentor, unidade, hostname
e IMEI, mas **não** por `IdentificacaoInterna`. Os resultados também exibem hostname/série em vez
da identificação exibida. A busca da lista (Enter) procura nela.

**Impacto:** a TI identifica os ativos pelo rótulo próprio (ex.: `MAQ-0123`). Digitar esse rótulo
na barra não traz nada, justo o jeito mais natural de procurar.

**Onde:** `src/Tracker.Api/Controllers/BuscaController.cs` (`BuscarDispositivos`, que monta os
`EF.Functions.Like`) e `src/Tracker.Web/src/features/busca/BarraDeBusca.tsx`.

**Correção sugerida:**

- Incluir `EF.Functions.Like(d.IdentificacaoInterna, padrao, "\\")` no filtro da base.
  `IdentificacaoInterna` fica em `Dispositivo`, então vale para todos os tipos.
- Devolver o campo no DTO do resultado e exibir com `identificacaoExibida`.
- A impressora entrou na busca (com consulta própria) e também está sem o campo: inclua-o lá.

**Verificar:** um teste em `Tracker.Api.Tests` que busca por um rótulo `MAQ-*` e encontra.

---

## 9. Relatórios ausentes no menu do celular

**Problema:** o item **Relatórios** só existe no menu da conta do desktop. A gaveta lateral do
celular não tem.

**Onde:** `src/Tracker.Web/src/layout/AppShell.tsx` (gaveta) e
`src/Tracker.Web/src/features/relatorios/MenuRelatorios.tsx`.

**Correção sugerida:** acrescentar os relatórios na gaveta, gateados por `dispositivos.ver`.
O `MenuRelatorios` agora tem **dois níveis** (relatório → formato, com 4 relatórios, incluindo
o de termo de uso): submenus ancorados não funcionam bem dentro de um `Drawer`, então a gaveta
precisa de uma lista própria (ex.: `Collapse` por relatório) ou de uma página de relatórios.
Também dá para aceitar como limitação: o XLSX é pouco útil no celular. Nesse caso, registre a
decisão aqui e remova o item.

---

## 10. Detalhe de arquivado mostra "não encontrado"

**Problema:** clicar numa linha da aba *Arquivados* abre o detalhe, que mostra "Dispositivo não
encontrado." porque o `GET /api/dispositivos/{id}` aplica o filtro global de arquivamento.

**Impacto:** não dá para consultar o histórico de um arquivado (movimentações, termos, motivo)
sem restaurá-lo. Isso ainda o faz voltar ao inventário e aos relatórios.

**Correção sugerida (escolha uma):**

- **Mínima:** na aba *Arquivados*, a linha não navega. Só os botões de ação ficam.
- **Completa:** o `GET` do detalhe aceita arquivados (`IgnoreQueryFilters()` apenas nesse
  endpoint), e a tela mostra um aviso "Arquivado em … por … — motivo", ações de edição ocultas e
  um botão **Restaurar**. As abas de histórico precisam do mesmo tratamento.

  > Cuidado: cada consulta filha (movimentações, reparos, termos) também passa pelo filtro. Veja
  > a nota sobre `HasQueryFilter` no `CLAUDE.md`.

---

## 11. Status de comando "Falhou" nunca é gravado

**Problema:** `StatusComando.Falhou` existe no enum (`src/Tracker.Domain/Enums/Enums.cs`) e a
tela sabe exibi-lo, mas nenhum código do backend o grava. Um comando que o agente nunca confirma
fica **Pendente ou Enviado para sempre**.

**Impacto:** a TI não tem como saber que um bloqueio não pegou. O aparelho parece "a caminho de
bloquear" indefinidamente, o que é perigoso num caso de roubo.

**Correção sugerida:**

- Deixar o agente reportar falha ao confirmar o comando (ele já confirma o sucesso).
- E/ou marcar como `Falhou` o comando sem confirmação após um prazo, por exemplo N heartbeats
  recebidos depois do envio sem confirmação. Não use prazo fixo: um aparelho offline legitimamente
  não falhou.
- Exibir alerta no detalhe quando o último comando falhou.

**Risco a pesar antes de marcar `Falhou` por prazo (levantado em 13/10/2026):** comando `Falhou`
sai da reentrega do heartbeat **e** é ignorado por `UltimoComandoVigenteAsync`. Um Bloquear
marcado assim deixaria de chegar ao aparelho e o `deveEstarBloqueado` voltaria ao comando
anterior/status — num roubo, o aparelho ficaria livre. Alternativa mais segura: só gerar um
**alerta** "comando sem confirmação" após N heartbeats, mantendo o comando `Enviado`.

**Decisão a tomar:** política de prazo e se o sistema reenvia automaticamente. Mexe no protocolo
dos **dois** agentes (Windows e Android). O Android exige `versionCode` novo e OTA.

---

## 12. Cancelar reparo deixa o aparelho em manutenção

**Problema:** abrir um reparo põe o aparelho em *Em manutenção*. **Concluir** o devolve a Em uso
ou Em estoque, mas **Cancelar** não mexe no status, e o aparelho fica em manutenção sem reparo
aberto.

**Onde:** `src/Tracker.Api/Controllers/DispositivosController.cs`, na atualização de status do
reparo (o trecho que trata `Concluido`).

**Correção sugerida:** tratar `Cancelado` como `Concluido` para o status do aparelho, quando não
houver outro reparo aberto. Conferir também o caso de dois reparos abertos ao mesmo tempo:
concluir um não deveria tirar o aparelho de manutenção enquanto o outro segue aberto.

---

## 13. Chave do CPF sem rotação

**Problema:** `AesStringEncryptor` trabalha com uma chave só. Trocar `Encryption__CpfKeyBase64`
deixa todos os CPFs gravados ilegíveis, e as telas que carregam detentores quebram. Detalhes em
[backup-e-chaves.md](../operacao/backup-e-chaves.md#encryption__cpfkeybase64-a-mais-crítica).

**Impacto:** se a chave vazar, não há como trocá-la.

**Correção sugerida:**

- Prefixar o texto cifrado com um identificador de versão da chave (ex.: `v2:`).
- Aceitar várias chaves na configuração (`Encryption:CpfKeys:{versao}`) e cifrar sempre com a atual.
- Criar um utilitário de linha de comando, no estilo do `--redefinir-senha-adm`, que relê e
  recifra todos os CPFs.
- Valores sem prefixo são tratados como `v1`, para compatibilidade com o banco atual.

**Verificar:** teste que grava com a chave v1, adiciona a v2 e confere que:

- o CPF antigo continua legível;
- um CPF novo sai cifrado com a v2;
- depois do utilitário, nenhum registro depende mais da v1.

---

## 14. Comentários desatualizados sobre relatórios no Perfil

> **Parcial — 08/10/2026.** **Resolvido:** os comentários de `MenuRelatorios.tsx` e `PerfilPage.tsx`.
> **Pendente:** `App.tsx` (rota de Perfil) e `AppShell.tsx` (menu da conta).

**Problema:** comentários em `src/Tracker.Web/src/App.tsx` e
`src/Tracker.Web/src/layout/AppShell.tsx` dizem que os relatórios também estão
na página de Perfil. Não estão: ficam só no menu da conta.

**Correção:** ajustar os comentários. É trivial e pode entrar em qualquer PR.

---

## 15. Localização gravada a cada minuto, para sempre

**Problema:** todo heartbeat com coordenadas insere uma linha em `HistoricoLocalizacoes`, sem
comparar com a anterior e sem expurgo — **~1.440 linhas por aparelho por dia**, indefinidamente.
A tela usa só a última.

**Impacto:** privacidade (trilha minuto a minuto do colaborador, inclusive fora do expediente, em
aparelho que ele leva para casa) e crescimento do banco e do backup.

**Onde:** `src/Tracker.Api/Controllers/AgenteController.cs` (heartbeat, `HistoricoLocalizacoes.Add`).

**Correção sugerida:**

- **Deduplicar:** só gravar ponto novo se o aparelho se moveu mais que N metros (acima da
  precisão) ou se passou X tempo desde o último ponto.
- **Retenção:** rotina (`BackgroundService`) que apaga os pontos mais velhos que o prazo
  definido. Use `ExecuteDeleteAsync` em lotes, porque a tabela é grande.
- **Decisão a tomar (jurídico):** prazo de retenção, e se a coleta precisa mesmo ser contínua ou
  pode acontecer só quando o aparelho for dado como perdido.

**Verificar:** teste que manda dois heartbeats na mesma posição e espera uma linha só.

---

## 16. CPF completo para quem só vê o inventário

**Problema:** `GET /api/usuarios` pede só `dispositivos.ver`, porque alimenta o diálogo de
atribuição, e devolve o **CPF inteiro**. A tela *Usuários* o mostra sem máscara.

**Impacto:** a permissão mais básica do sistema dá acesso ao CPF de todos os colaboradores.

**Onde:** `src/Tracker.Api/Controllers/UsuariosController.cs` (`Listar`, `UsuarioDto`) e
`src/Tracker.Web/src/pages/UsuariosPage.tsx`.

**Correção sugerida:**

- Devolver o CPF **mascarado** (`***.456.789-**`) na listagem.
- A geração do termo já busca o CPF no servidor e não precisa dele no front.
- Se alguém precisar ver o CPF completo, criar uma permissão própria (valor novo no **fim** de
  `Permissoes.Todas`) com auditoria de leitura.

**Verificar:** teste de API em que um login só com `dispositivos.ver` não recebe o CPF completo
e o PDF do termo continua saindo com o CPF certo.

---

## 17. Geolocalização do Windows por HTTP em serviço gratuito

**Problema:** o agente Windows consulta `http://ip-api.com/json/...` sem TLS, num serviço externo
sem contrato. Pelo que se sabe do serviço, o plano gratuito não oferece HTTPS e restringe uso
comercial. **Confirme nos termos atuais do serviço.**

**Impacto:**

- o IP de cada notebook trafega em claro para um terceiro a cada minuto;
- a resposta pode ser adulterada no caminho;
- há possível violação dos termos de uso do serviço.

**Onde:** `agents/Tracker.Agent.Windows/Geolocalizacao.cs`.

**Correção sugerida (escolha uma):**

- Provedor contratado com HTTPS.
- O **servidor** faz a geolocalização pelo IP de origem do heartbeat, e o notebook deixa de falar
  com terceiros. Antes, confira o IP que chega atrás de NAT (item 20).
- Reduzir a frequência da consulta, já que o IP muda pouco.

Qualquer opção exige nova versão do agente Windows, com instalador regerado e reinstalação nos
notebooks.

---

## 18. Sem exclusão/anonimização de detentor

**Problema:** "Desativar" só marca `Ativo = false`, e não há como eliminar os dados de um
ex-colaborador. As FKs `Restrict` de Termo e Movimentação impedem um `DELETE` simples.

**Impacto:** um pedido de eliminação do titular só se atende mexendo direto no banco.

**Correção sugerida:** **anonimizar** em vez de apagar:

- trocar nome, e-mail, telefone e CPF por marcadores (ex.: "Ex-colaborador #1234");
- preservar o vínculo com termos e histórico, que podem ter obrigação legal de guarda;
- pedir a senha do ADM e registrar na auditoria.

**Decisão a tomar (jurídico):** o destino dos PDFs dos termos, que trazem o CPF impresso, e do
texto livre de comentários e motivos.

---

## 19. Token do agente na URL e em texto puro

**Problema:**

- A conexão SignalR passa o token na **query string** (`?token=...`), que pode ficar gravada em
  log do IIS ou de proxy.
- No Windows, o `token.dat` fica em **texto puro**.
- No Android, se o armazenamento cifrado falhar, o app cai para o armazenamento comum sem avisar.

**Onde:** `agents/Tracker.Agent.Windows/Worker.cs` e `EnrollmentService.cs`;
`src/Tracker.Api/Agente/ComandoRemotoHub.cs`; Android `dados/Armazenamento.kt`.

**Correção sugerida:**

- **Query string:** tirá-la dos logs do IIS, ou mandar o token por header no *negotiate*, se o
  transporte usado permitir (o WebSocket só aceita query). Valide nos dois clientes.
- **`token.dat`:** proteger com **DPAPI** (`ProtectedData`, escopo máquina) e ACL só para SYSTEM
  e Administradores.
- **Android:** gerar um alerta quando o app cair no armazenamento comum.

Mudar o formato do `token.dat` exige compatibilidade: o agente novo precisa ler o arquivo antigo,
senão a frota inteira se recadastra de uma vez.

---

## 20. Conferir o IP gravado na auditoria

**Problema:** a auditoria grava `Connection.RemoteIpAddress` e o app não usa
`UseForwardedHeaders`. No IIS in-process o IP do cliente costuma chegar certo. Mas, se houver
NAT, proxy ou firewall reescrevendo a origem, todos os registros sairão com o mesmo IP.

**Verificar primeiro:** na tela *Auditoria*, os logins de pessoas diferentes mostram IPs
diferentes e coerentes? Se sim, feche o item sem mudança. Se todos forem iguais, configure
`ForwardedHeaders` **com a lista de proxies confiáveis**. Sem essa lista, qualquer um forja o IP
pelo header `X-Forwarded-For`.

---

## 21. HSTS não configurado na aplicação

**Problema:** `Program.cs` tem `UseHttpsRedirection`, mas não `UseHsts`.

**Correção:**

1. Confira se o IIS já envia `Strict-Transport-Security`, com
   `curl -I https://tracker.foodsacademy.com.br`.
2. Se não enviar, use `app.UseHsts()` fora de Development.

Cuidado: o domínio `foodsacademy.com.br` é compartilhado. **Não** use `includeSubDomains` nem
`preload` sem alinhar com quem cuida do FOOD-ACADEMY.

---

## 22. Agente Windows ignora "deve estar bloqueado"

> ✅ **Resolvido em 13/10/2026 (PR #48).** `HeartbeatResposta`
> lê `deveEstarBloqueado`; `ExecutorDeComandos.SincronizarBloqueioAsync` recria o `locked.flag` e
> reporta `BloqueioReaplicado` (novo `BackendClient.ReportarAlertaAsync`) quando o servidor diz
> bloqueado e o sinal sumiu, e remove a trava quando o servidor diz desbloqueado — mesma regra do
> Android, com o contador `_geracaoComandos` descartando resposta mais velha que o último comando
> (senão um Desbloquear do hub seria desfeito por um heartbeat em voo). Com `Simular=true` só loga.
> Chega à frota só **reinstalando** com o `.exe` novo (não há OTA no Windows).

**Problema:** a resposta do heartbeat traz `deveEstarBloqueado`, e o Android reaplica o bloqueio
quando ele vem verdadeiro (gera o alerta *Bloqueio reaplicado*). O agente Windows lê só a lista
de comandos. Se o arquivo-sinal `locked.flag` sumir (limpeza, restauração, alguém com acesso de
administrador), um notebook que o servidor considera **Bloqueado** volta a funcionar e o painel
não fica sabendo.

**Onde:** `agents/Tracker.Agent.Windows/BackendClient.cs` (`HeartbeatResposta` só com a lista de
comandos) e `Worker.cs`.

**Correção sugerida:**

- Ler `deveEstarBloqueado` e, se for verdadeiro e o sinal local não existir, recriar o sinal e
  subir a tela de bloqueio.
- Reportar o alerta `BloqueioReaplicado` (o endpoint `/api/agente/alertas` já existe; o Windows
  hoje não reporta alerta nenhum).

Exige nova versão do agente Windows.

---

## 23. Agente Android não se recadastra sozinho

**Problema:** quando o servidor rejeita o token (401), o agente Windows se recadastra sozinho. O
Android apaga o token e fica esperando alguém registrar de novo no aparelho.

**Impacto:** após restaurar um backup antigo, regenerar o token no painel ou trocar de servidor,
**cada celular e tablet** precisa que um técnico pegue o aparelho e refaça o registro.

**Por que não é automático hoje:** o código de instalação não é guardado no aparelho, de
propósito, para não ficar exposto num aparelho perdido. Sem ele, não há como recadastrar.

**Correção sugerida:**

- Guardar no armazenamento cifrado uma **credencial de recadastro** específica do aparelho, e
  não o código da empresa.
- Ou aceitar o recadastro com o token antigo durante uma janela, depois de validado no servidor.

Precisa de desenho de segurança antes de implementar. Mexe no protocolo e exige `versionCode`
novo.

---

## 24. Agente Windows informa sempre a versão 1.0.0

> ✅ **Resolvido em 13/10/2026 (PR #48).** `Worker.Versao` lê a
> `AssemblyInformationalVersion`. O `gerar-pendrive.ps1` ganhou `-Versao` (o CI passa a tag;
> sem ela, `git describe --tags`; sem git, `0.0.0-dev`) e publica com
> `-p:InformationalVersion=...`. O `.csproj` desliga o sufixo `+<commit>` do SDK. Notebooks com
> instalador antigo seguem mandando `1.0.0` até serem reinstalados.

**Problema:** o agente Windows manda `agenteVersao = "1.0.0"` fixo no código, em qualquer release.

**Impacto:** a coluna de versão do agente no painel não serve para saber quais notebooks rodam o
instalador antigo, justo a informação necessária para decidir quem reinstalar (a frota Windows
não tem OTA).

**Onde:** `agents/Tracker.Agent.Windows/Worker.cs` (constante de versão).

**Correção sugerida:**

- Ler a versão do assembly (`Assembly.GetEntryAssembly().GetName().Version`, ou
  `AssemblyInformationalVersion`).
- Carimbar essa versão no build do instalador, em `gerar-pendrive.ps1`, com a tag da release.

---

## 25. Inconsistências menores nos agentes

> **Parcial — 24/09/2026 (revisão completa do código).** **Resolvido:** o comentário de
> `AgenteOptions.cs` não sugere mais `"Telefone"` e (conferido em 08/10/2026) os comentários de
> 2 min do `AgenteService.kt`. **Pendente:** o default `Simular = true` no código, o token sem
> URL-encode e a origem fixa `GpsAndroid`.

Nenhuma afeta o funcionamento. Corrija quando mexer nos arquivos.

- `agents/Tracker.Agent.Windows/AgenteOptions.cs`: o default de `Simular` é **true** no código e
  **false** no `appsettings.json`: se o arquivo perder a chave, o agente para de bloquear de verdade
  sem avisar.
- Android: o token vai na URL do hub sem URL-encode. É inofensivo com base64url, mas vale
  codificar por consistência com o Windows.
- Android: a origem da localização é sempre `GpsAndroid`, mesmo quando a posição veio da rede.

---

## 26. Trocar o código de instalação vazado no histórico do Git

**Problema:** o valor **real** de produção do `Enrollment:CompanyKey` (o código de instalação dos
agentes) foi escrito no `docs/agentes/agente-android.md` e entrou no commit `e36d642`
(13/08/2026). O arquivo já usa o marcador `<CODIGO-DO-COFRE>`, mas **o valor continua no
histórico do Git**: qualquer clone, fork ou cópia antiga do repositório o recupera com `git log`.

**Impacto:** quem tiver o código cadastra aparelhos falsos no inventário e recebe token de
agente. Tirar do arquivo **não basta**: a chave tem de ser considerada vazada.

**Onde:** variável `Enrollment__CompanyKey` do servidor (API **e** runner do CI), o
`Instalar-Agente.exe` do agente Windows (embute a chave) e o cofre de onde os técnicos copiam o
código para o registro Android.

**Correção sugerida:**

1. Trocar a chave pelo procedimento **"Trocar (se vazar, ou por rotina)"** de
   [backup-e-chaves.md](../operacao/backup-e-chaves.md#enrollment__companykey-o-código-de-instalação-dos-agentes).
2. Regerar o instalador Windows. O CI **não** regera sozinho numa release que não mexa no
   agente: apague `C:\TrackerAgentDist\ultima-geracao.txt` antes da próxima tag (força a
   regeração) ou rode o `gerar-pendrive.ps1` à mão. Descarte os `.exe` antigos.
3. Reescrever o histórico (`git filter-repo`) é **opcional** e caro (invalida todos os clones).
   Sem a troca da chave ele não resolve nada; com a troca, deixa de ser necessário.

**Nunca escreva o valor, nem o antigo, em doc, commit, issue ou chat** ao executar isto.

**Verificar:** um auto-cadastro com o código antigo recebe 401 "Chave de instalação inválida.";
os aparelhos já cadastrados seguem com heartbeat normal (usam o token, não a chave); um
`Instalar-Agente.exe` novo cadastra uma máquina de teste.

---

## 27. Android bloqueado com token revogado fica preso

**Problema:** desde o agente 1.1.3 o bloqueio no Android é reaplicado (boot, `VigilanteWorker`,
abertura do app) só pelo sinal `bloqueado` persistido, **independente do token**. É de
propósito: um aparelho bloqueado não pode voltar livre quando o token some. Mas o 401 apaga o
token, e o agente Android não se recadastra sozinho (item 23). Resultado: aparelho
**bloqueado + token revogado** (token regenerado no painel, backup antigo restaurado, servidor
trocado) fica bloqueado **para sempre**. O Desbloquear não chega (sem token não há heartbeat nem
hub) e a sobreposição cobre a tela de registro, onde o técnico digitaria o código.

**Onde:** `agents/Tracker.Agent.Android/.../servico/AgenteService.kt` (laço de heartbeat sem
token), `servico/BootReceiver.kt`, `servico/VigilanteWorker.kt`, `TrackerApp.kt` e
`dados/Armazenamento.kt` (`bloqueado` no arquivo `tracker-agente-estado`).

**Saída hoje:** só por `adb`, com o aparelho na mão da TI. Ver
[agente-android.md](../agentes/agente-android.md#bloqueado-e-sem-token-aparelho-preso).

**Correção sugerida (escolha uma):**

- Com o aparelho bloqueado **e** sem token, a sobreposição oferece o registro (campo do código de
  instalação). Só a TI tem o código, então isso não libera o usuário; depois do registro, o
  `deveEstarBloqueado` do servidor decide.
- Resolver o item 23 (recadastro automático), que elimina a causa.

Mexe no agente: `versionCode` novo e OTA.

**Verificar:** em aparelho de laboratório, bloquear, regenerar o token no painel e conferir que
a TI consegue registrar de novo e desbloquear sem `adb`.

---

## 28. Máquinas Windows com serial genérico duplicam ao recadastrar

**Problema:** o agente Windows passou a descartar serial genérico de BIOS ("To be filled by
O.E.M.", "Default string" etc.) e a mandar `WIN-<UUID do SMBIOS>`, depois `WIN-<MachineGuid>` e,
por último, `HOST-<nome>`. Uma máquina **já cadastrada** com serial genérico ou com `HOST-<nome>`
passa a se apresentar com outro serial. Enquanto o `token.dat` vale, nada muda. Mas se ela **se
recadastrar** (401, `token.dat` apagado), o reencontro (patrimônio, série, IMEI) não a acha,
porque o agente Windows não envia patrimônio, e ela entra como **registro novo**. Máquinas
diferentes que já dividiam o mesmo registro por causa do serial genérico também se separam
nesse momento.

**Onde:** `agents/Tracker.Agent.Windows/InfoMaquina.cs` (fallback do serial) e
`AgenteController.ReencontrarAsync`.

**Correção sugerida:**

- Por ora, limpar à mão: arquivar o registro que sobrar e acertar identificação interna,
  detentor e unidade no que ficar.
- Definitiva: no auto-cadastro com serial `WIN-*` sem reencontro, procurar um `Notebook` cujo
  serial seja genérico (`SerialGenerico.EhGenerico`) ou `HOST-<mesmo hostname>` **e** com o
  mesmo MAC, e promover o serial em vez de criar outro.

**Verificar:** listar os candidatos antes de a frota se recadastrar (`NumeroSerie LIKE 'HOST-%'`
ou valores genéricos) e, depois, procurar hostnames repetidos na lista de dispositivos.

---

## 29. Relatórios convertem a data pelo fuso do servidor

> **Parcial — 08/10/2026.** O relatório de **termos** (novo) já usa `FusoBrasil`. Falta o
> "Cadastrado em" dos outros três.

**Problema:** o "Cadastrado em" dos relatórios usa `ToLocalTime()`, que depende do fuso
configurado na máquina. O termo de uso e o filtro da auditoria já usam o helper `FusoBrasil`
(`src/Tracker.Application/Common/FusoBrasil.cs`), fixo em Brasília.

**Impacto:** nenhum enquanto o servidor estiver no fuso de Brasília. Num servidor em UTC (ou num
runner de teste), cadastro feito à noite sai com a data do dia seguinte.

**Onde:** `src/Tracker.Infrastructure/Relatorios/RelatorioInventarioService.cs`
(`CadastradoEm`).

**Correção:** trocar por `FusoBrasil.ParaBrasilia(d.CriadoEm)`.

**Verificar:** teste que gera o relatório com um `CriadoEm` às 01:00Z e espera a data do dia
anterior, independente do fuso da máquina.

---

## 30. Heartbeat sem a data da leitura da localização

**Problema:** o `HeartbeatRequest` não tem campo com o momento em que a posição foi lida, e o
servidor grava o ponto com a hora do heartbeat. O agente Android 1.1.3 mitiga descartando a
última posição conhecida com mais de 5 minutos, mas uma leitura de até 5 minutos atrás ainda
aparece como "agora".

**Onde:** `src/Tracker.Application/Dtos/AgenteDtos.cs` (`HeartbeatRequest`),
`AgenteController.Heartbeat` e `localizacao/Geolocalizacao.kt` no Android.

**Correção sugerida:** campo opcional `localizacaoLidaEm` (UTC) no request, gravado no ponto
quando vier. Sem ele, vale a hora do heartbeat (compatível com o agente Windows e com agentes
Android antigos). Atualizar o [protocolo-agente.md](../agentes/protocolo-agente.md).

---

## 31. DisableTaskMgr removido em todo logon

**Problema:** a tela de bloqueio do Windows liga `DisableTaskMgr` no `HKCU` do usuário. Para
nunca deixar o Gerenciador de Tarefas desligado depois do desbloqueio, o overlay sem
`locked.flag` só **remove** o valor e sai. Isso roda em todo logon (tarefa `TrackerAgentLock`) e
depois de cada desbloqueio, e a remoção é **incondicional**.

**Impacto:** se uma GPO da empresa definir `DisableTaskMgr` para o usuário, o agente a desfaz a
cada logon, até a próxima atualização de política.

**Onde:** `agents/Tracker.Agent.Windows/LockOverlay.cs` (limpeza) e `BloqueioTela.cs`.

**Correção sugerida:** gravar um marcador próprio quando o **agente** liga o valor e só remover
se o marcador existir; ou conferir se há política em `HKCU\Software\Policies`.

**Verificar:** com a GPO aplicada, fazer logon e conferir que o Gerenciador continua desabilitado;
bloquear, desbloquear e conferir que volta ao estado da GPO.

---

## 32. Atribuir aparelho Bloqueado: só o site recusa

**Problema:** o site esconde **Atribuir** para *Baixado*, *Em manutenção* e *Bloqueado*
(`statusPermiteAtribuir`, em `src/Tracker.Web/src/features/dispositivos/status.ts`), mas o
`DispositivosController.Atribuir` só recusa *Baixado* e *Em manutenção*. Com *Bloqueado* ele
aceita, troca o detentor e mantém o status *Bloqueado*. A regra do projeto é gatear os dois lados.

**Impacto:** baixo, porque só acontece por chamada direta à API. Mas o espelho da regra está
incompleto.

**Correção:** no `Atribuir`, recusar *Bloqueado* com 400 e a mesma mensagem do front
("Dispositivo bloqueado. Desbloqueie pelo comando remoto antes de atribuir.").

**Verificar:** teste de integração que atribui um dispositivo *Bloqueado* e espera 400.

---

## 33. Aparelho perdido fica Bloqueado para sempre

> ✅ **Resolvido em 24/09/2026 (revisão completa do código).** `AtualizarStatus` aceita **Bloqueado →
> Baixado** (só esse destino); o menu de status do site oferece só "Dar baixa" num Bloqueado; um
> Bloquear confirmado depois da baixa mantém `Baixado`. Coberto por
> `RegrasDispositivoTests.BaixaDeAparelhoBloqueado_NaoDestrava`. Arquivar continua recusado com
> Bloquear a caminho.

**Problema:** desde a revisão de 24/09/2026, `AtualizarStatus` recusa qualquer mudança em
aparelho *Bloqueado*, e a exclusão/arquivamento já recusava. O status só sai de *Bloqueado*
quando o **agente confirma** um Desbloquear. Um aparelho roubado ou perdido que nunca mais se
conecta, portanto, **não recebe baixa nem pode ser arquivado**: fica *Bloqueado* no inventário,
no dashboard e nos relatórios. Antes da revisão, o manual orientava "mantenha bloqueado e dê
baixa" — hoje esse passo é recusado.

**Onde:** `src/Tracker.Api/Controllers/DispositivosController.cs` (`AtualizarStatus` e
`Excluir`).

**Correção sugerida:** permitir **Dar baixa** num *Bloqueado* (só esse destino), mantendo o
último comando como Bloquear: o `deveEstarBloqueado` do heartbeat segue o último comando, não o
status, então se o aparelho reaparecer ele continua travado. Pedir motivo e auditar. Arquivar
um *Baixado* que ainda tem Bloquear vigente é decisão à parte (a trava da exclusão existe para
não sumir com o único botão de Desbloquear).

**Verificar:** teste que bloqueia (comando confirmado), dá baixa e confere que o heartbeat
seguinte ainda responde `deveEstarBloqueado = true`.

---

## 34. Agente pode postar alerta de impressora

**Problema:** o validador de `POST /api/agente/alertas` só confere `IsInEnum`. Os tipos
`ImpressoraOffline` e `SuprimentoBaixo` são gerados pelo servidor (SNMP), mas um agente consegue
postá-los num notebook/celular, e o alerta aparece com rótulo de impressora.

**Onde:** `src/Tracker.Application/Validators/AgenteValidators.cs` (validador do alerta).

**Correção sugerida:** recusar (400) os tipos de impressora nesse endpoint — os tipos aceitos do
agente são `AdminDesativado`, `LocalizacaoNegada` e `BloqueioReaplicado`. Atualizar a tabela do
§5 de [protocolo-agente.md](../agentes/protocolo-agente.md).

**Verificar:** teste que posta `ImpressoraOffline` com token de agente e espera 400.

---

## 35. `@microsoft/signalr` instalado no site sem uso

**Problema:** o `package.json` do `Tracker.Web` traz `@microsoft/signalr`, mas nada em
`src/Tracker.Web/src` o importa. O site não ouve o hub (só os agentes ouvem). É dependência a
mais para instalar, atualizar e auditar.

**Correção sugerida:** remover a dependência (e o `package-lock.json` muda, o que invalida o
cache de `node_modules` do CI uma vez). Se um dia o painel for atualizar em tempo real, ela volta.

**Verificar:** `npm --prefix src/Tracker.Web run build` passa sem ela.

---

## 36. Instalador do agente na NETLOGON expõe o código de instalação

**Problema:** a GPO **Deploy Tracker Agent** distribuía o `Instalar-Agente.exe` a partir de
`\\NHNTAPP001\netlogon\agentTracker`. A NETLOGON é legível por **qualquer usuário do domínio**, e
o `.exe` traz o `Enrollment__CompanyKey` num `.zip` interno: qualquer pessoa o extrai com o 7-Zip.

**Impacto:** com o código, dá para cadastrar aparelhos falsos no inventário e, sabendo a série ou o
patrimônio de um aparelho real, gerar um token novo para ele pelo auto-cadastro (o agente real
recebe 401 e se recadastra, mas o painel passa a receber dados de quem quiser).

**Onde:** fora do código, na pasta da GPO. O fluxo novo está em
[atualizacao-agente-gpo.md](../agentes/atualizacao-agente-gpo.md).

**Correção sugerida:**

1. Compartilhar `F:\AgentTracker` do servidor `.254` como `\\192.168.0.254\AgentTracker`, com
   leitura só para `Computadores do domínio` (passo 1 do guia), e trocar o `gpo_installer.cmd`
   da NETLOGON pelo do repositório.
2. Apagar o `Instalar-Agente.exe` de `\\NHNTAPP001\netlogon\agentTracker`.
3. **Trocar o `Enrollment__CompanyKey`** (o valor ficou exposto): junta com o item 26.

**Verificar:** logado como colaborador comum, `dir \\192.168.0.254\AgentTracker` dá acesso negado e
`\\NHNTAPP001\netlogon\agentTracker` não tem mais `.exe`.

---

## 37. Agente Windows sem atualização automática própria

**Problema:** o Android se atualiza pelo heartbeat (OTA). O Windows depende da GPO, que só roda
**no boot** e **com a rede da empresa** ([atualizacao-agente-gpo.md](../agentes/atualizacao-agente-gpo.md)).

**Impacto:** notebook que só hiberna, ou que fica fora do escritório sem VPN antes do logon,
demora ou nunca recebe a versão nova (correções de bloqueio inclusive).

**Correção sugerida:** OTA como a do Android: a TI publica o instalador no painel, o heartbeat
anuncia a versão (o agente já envia `agenteVersao`), o agente baixa com o próprio token, confere o
SHA-256 e roda `Instalar-Agente.exe --silencioso` fora do próprio processo (o instalador para o
serviço). O pacote embute o código de instalação: o download tem de exigir token de agente válido.

**Verificar:** publicar versão nova no painel e ver um notebook fora da rede da empresa mudar de
versão sem reiniciar.
