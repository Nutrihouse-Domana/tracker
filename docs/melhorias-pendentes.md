# Melhorias pendentes

Backlog de limitações conhecidas do Tracker, levantado em 23/09/2026 na revisão da documentação.
Cada item diz o problema, onde está no código, a correção sugerida e como conferir.

**Ao corrigir um item:**

1. Marque-o como resolvido aqui, com data e PR.
2. Remova-o da seção "Limitações conhecidas" do [manual-do-usuario.md](manual-do-usuario.md) e
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
| 2 | [Restaurar e desativar detentor sem confirmação](#2-restaurar-e-desativar-detentor-sem-confirmação) | Média | Pequeno |
| 3 | [Detentor desativado não pode ser reativado](#3-detentor-desativado-não-pode-ser-reativado) | **Alta** | Pequeno |
| 4 | ["Excluir definitivamente" visível sem a permissão](#4-excluir-definitivamente-visível-sem-a-permissão) | Média | Pequeno |
| 5 | [Atribuir e Alterar status escondem o motivo do erro](#5-atribuir-e-alterar-status-escondem-o-motivo-do-erro) | Média | Pequeno |
| 6 | [Login não distingue desativado/bloqueado](#6-login-não-distingue-desativado-ou-bloqueado) | Baixa | Pequeno |
| 7 | [Rótulos técnicos sem tradução](#7-rótulos-técnicos-sem-tradução) | Baixa | Pequeno |
| 8 | [Busca global ignora a identificação interna](#8-busca-global-ignora-a-identificação-interna) | Média | Pequeno |
| 9 | [Relatórios ausentes no menu do celular](#9-relatórios-ausentes-no-menu-do-celular) | Baixa | Pequeno |
| 10 | [Detalhe de arquivado mostra "não encontrado"](#10-detalhe-de-arquivado-mostra-não-encontrado) | Baixa | Médio |
| 11 | [Status de comando "Falhou" nunca é gravado](#11-status-de-comando-falhou-nunca-é-gravado) | Média | Médio |
| 12 | [Cancelar reparo deixa o aparelho em manutenção](#12-cancelar-reparo-deixa-o-aparelho-em-manutenção) | Baixa | Pequeno |
| 13 | [Chave do CPF sem rotação](#13-chave-do-cpf-sem-rotação) | Média | Grande |
| 14 | [Comentários desatualizados sobre relatórios no Perfil](#14-comentários-desatualizados-sobre-relatórios-no-perfil) | Baixa | Trivial |

**Sugestão de ordem:**

1. Itens **1 e 3**: são os que mais afetam o dia a dia.
2. Itens **2, 4 e 5**: mesmo tipo de mudança, cabem num PR só.
3. Itens **8 e 11**.
4. Os demais, conforme houver tempo.

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

**Problema:** alguns valores de enum aparecem crus na tela.

| Onde aparece | Valores |
|---|---|
| Status do reparo (`ReparosSection.tsx`) | `EmAndamento`, `Concluido` |
| Histórico de comandos (`AgenteSection.tsx`) | `Bloquear`, `Desbloquear`, `Pendente`, `Enviado`, `Confirmado` |
| Origem da localização (`LocalizacaoSection.tsx`) | `GpsAndroid`, `IpGeolocationWindows`, `WifiConhecida`, `ManualTI` |
| Auditoria (`AuditoriaPage.tsx`) | a ação de vencimento de aluguel (`DispositivoVencimentoAluguelDefinido`) aparece pelo nome técnico, no filtro e no chip |

**Correção sugerida:** mapas de rótulo, como os que já existem para o status do dispositivo. Na
auditoria, basta acrescentar a ação ao mapa de rótulos da página. Vale criar um teste ou uma
checagem de tipo que falhe quando um valor novo de enum ficar sem rótulo, porque foi exatamente
assim que a ação do vencimento escapou.

---

## 8. Busca global ignora a identificação interna

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

**Verificar:** um teste em `Tracker.Api.Tests` que busca por um rótulo `MAQ-*` e encontra.

---

## 9. Relatórios ausentes no menu do celular

**Problema:** o item **Relatórios** só existe no menu da conta do desktop. A gaveta lateral do
celular não tem.

**Onde:** `src/Tracker.Web/src/layout/AppShell.tsx` (gaveta) e
`src/Tracker.Web/src/features/relatorios/MenuRelatorios.tsx`.

**Correção sugerida:** acrescentar os relatórios na gaveta, gateados por `dispositivos.ver`.
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
[backup-e-chaves.md](backup-e-chaves.md#encryption__cpfkeybase64-a-mais-crítica).

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

**Problema:** comentários em `src/Tracker.Web/src/App.tsx` e
`src/Tracker.Web/src/features/relatorios/MenuRelatorios.tsx` dizem que os relatórios também estão
na página de Perfil. Não estão: ficam só no menu da conta.

**Correção:** ajustar os comentários. É trivial e pode entrar em qualquer PR.
