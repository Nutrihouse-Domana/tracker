package br.com.nutrihouse.tracker.agent.bloqueio

import android.app.ActivityManager
import android.content.Context
import android.content.Intent
import android.os.SystemClock
import android.util.Log
import br.com.nutrihouse.tracker.agent.dados.Armazenamento
import br.com.nutrihouse.tracker.agent.dispositivo.Politica

/**
 * Aplica e remove o bloqueio do aparelho.
 *
 * O bloqueio e **persistente**, igual ao do agente Windows: fica gravado localmente
 * (Armazenamento.bloqueado) e e reaplicado por um ciclo curto no AgenteService, inclusive apos
 * reiniciar e sem rede. Somente a TI o remove, pelo comando Desbloquear.
 *
 * O objetivo e deixar o aparelho **indisponivel**, nao apenas travar a tela. Trava de tela nao
 * resolve: o usuario digita a senha dele e volta a usar o aparelho.
 *
 * Por isso o mecanismo principal e a janela de sobreposicao (OverlayBloqueio), que fica acima de
 * qualquer aplicativo e consome os toques. `lockNow()` e usado **uma unica vez** ao aplicar, so
 * para apagar a tela na hora; a partir dai quem sustenta o bloqueio e a sobreposicao, reaplicada
 * em ciclo curto pelo AgenteService. Com Device Owner somamos o modo quiosque, que fecha tambem
 * o botao Home.
 */
class Bloqueio(private val context: Context) {

    private val armazenamento by lazy { Armazenamento(context) }
    private val politica by lazy { Politica(context) }

    val ativo: Boolean get() = armazenamento.bloqueado

    /** Marca o bloqueio como desejado e o aplica imediatamente. */
    fun aplicar() {
        armazenamento.bloqueado = true
        ultimaTentativaQuiosqueMs = 0L // entra em quiosque ja, sem esperar o intervalo
        garantirAtivo()

        // Uma vez so: apaga a tela agora, para o aparelho sair da mao do usuario imediatamente.
        // Repetir isso em ciclo daria a impressao de "so travou a tela", que nao e o objetivo.
        politica.travarTela()
    }

    /** Remove o bloqueio, devolvendo o aparelho ao uso. */
    fun remover() {
        armazenamento.bloqueado = false
        ultimaTentativaQuiosqueMs = 0L
        OverlayBloqueio.esconder(context)
        // Fecha tambem a Activity (quiosque do Device Owner, ou reforco sem sobreposicao): ela
        // chama stopLockTask() antes de sair.
        context.sendBroadcast(Intent(ACAO_FECHAR_TELA).setPackage(context.packageName))
        // Tirar o pacote da lista de lock task tambem encerra o quiosque se a Activity ja tiver
        // morrido e nao puder chamar stopLockTask() ela mesma.
        politica.definirQuiosque(ativo = false)
        Log.i(TAG, "Bloqueio removido.")
    }

    /**
     * Reaplica o bloqueio se ele deveria estar ativo. Chamado em ciclo curto pelo servico — e o
     * que sustenta o bloqueio depois de reiniciar, offline, ou se o sistema derrubar a janela.
     */
    fun garantirAtivo() {
        if (!armazenamento.bloqueado) return

        // Durante o uso do painel de Wi-Fi do sistema a sobreposicao precisa sair da frente,
        // senao o usuario nao consegue tocar na lista de redes.
        if (suspensoParaWifi) return

        // Com Device Owner, o modo quiosque fecha o que a sobreposicao nao alcanca: barra de
        // status, configuracoes rapidas, Home e Recentes. So uma Activity entra em lock task,
        // entao a BloqueioActivity sobe POR BAIXO da sobreposicao — as duas juntas.
        if (politica.ehDeviceOwner) garantirQuiosque()

        if (politica.podeSobreporJanelas) {
            OverlayBloqueio.mostrar(context)
            return
        }

        // Sem permissao de sobreposicao nao ha como cobrir outros apps. Cai para a Activity, que
        // ao menos avisa o usuario, e registra o motivo: e uma falha de instalacao a corrigir.
        Log.e(TAG, "Sem permissao de sobreposicao — aparelho NAO fica indisponivel. Conceda a permissao no app.")
        abrirTela()
    }

    /**
     * Garante a BloqueioActivity em lock task. Chamado a cada 2s pelo enforcement, entao so abre
     * a tela quando o aparelho NAO esta em quiosque, e no maximo a cada [INTERVALO_QUIOSQUE_MS]
     * — se o sistema recusar o lock task, nao ficamos relancando a Activity sem parar.
     */
    private fun garantirQuiosque() {
        val am = context.getSystemService(ActivityManager::class.java) ?: return
        if (am.lockTaskModeState != ActivityManager.LOCK_TASK_MODE_NONE) return

        val agora = SystemClock.elapsedRealtime()
        if (agora - ultimaTentativaQuiosqueMs < INTERVALO_QUIOSQUE_MS) return
        ultimaTentativaQuiosqueMs = agora

        // O pacote precisa estar autorizado a entrar em lock task (o Desbloquear o remove).
        politica.definirQuiosque(ativo = true)
        abrirTela()
    }

    private fun abrirTela() {
        val intent = Intent(context, BloqueioActivity::class.java).apply {
            addFlags(
                Intent.FLAG_ACTIVITY_NEW_TASK or
                    Intent.FLAG_ACTIVITY_CLEAR_TOP or
                    Intent.FLAG_ACTIVITY_SINGLE_TOP or
                    Intent.FLAG_ACTIVITY_NO_ANIMATION or
                    Intent.FLAG_ACTIVITY_EXCLUDE_FROM_RECENTS,
            )
        }

        runCatching { context.startActivity(intent) }
            .onFailure { Log.w(TAG, "Falha ao abrir a tela de bloqueio.", it) }
    }

    /**
     * Tira a sobreposicao da frente por um tempo curto, para o usuario conseguir usar o painel de
     * Wi-Fi do sistema. O bloqueio **nao** e removido: o sinal local continua ligado e o ciclo do
     * AgenteService recoloca a janela assim que a janela de tempo expira.
     *
     * E uma brecha consciente e limitada: sem rede, um aparelho bloqueado nunca receberia o
     * comando de Desbloquear, e a TI perderia o controle dele de vez. A partir do Android 10 o
     * painel e aberto por [PainelWifiActivity], que chama [retomarAposWifi] no instante em que o
     * usuario fecha o painel ou sai dele (Home, Recentes...) — o prazo aqui vira so um teto.
     */
    fun suspenderParaWifi() {
        if (!armazenamento.bloqueado) return

        suspensoAteMs = System.currentTimeMillis() + DURACAO_SUSPENSAO_MS
        OverlayBloqueio.esconder(context)
        Log.i(TAG, "Sobreposição suspensa por ${DURACAO_SUSPENSAO_MS / 1000}s para conexão Wi-Fi.")
    }

    /** Encerra a pausa para Wi-Fi antes do prazo e recoloca a trava na hora. */
    fun retomarAposWifi() {
        suspensoAteMs = 0L
        garantirAtivo()
    }

    companion object {
        private const val TAG = "Bloqueio"

        /**
         * Teto da pausa para Wi-Fi. No Android 10+ ela acaba antes, assim que o usuario deixa o
         * painel ([PainelWifiActivity]); so abaixo disso (tela cheia de Configuracoes, sem como
         * saber quando o usuario terminou) o aparelho fica este tempo todo sem a sobreposicao.
         */
        private const val DURACAO_SUSPENSAO_MS = 60_000L

        /** Espaco minimo entre tentativas de (re)abrir a tela em quiosque. */
        private const val INTERVALO_QUIOSQUE_MS = 10_000L

        @Volatile
        private var suspensoAteMs = 0L

        @Volatile
        private var ultimaTentativaQuiosqueMs = 0L

        /** Estado compartilhado: Bloqueio e instanciado sob demanda em varios pontos. */
        val suspensoParaWifi: Boolean
            get() = System.currentTimeMillis() < suspensoAteMs

        /** Broadcast interno que faz a BloqueioActivity se fechar quando a TI desbloqueia. */
        const val ACAO_FECHAR_TELA = "br.com.nutrihouse.tracker.agent.FECHAR_BLOQUEIO"
    }
}
