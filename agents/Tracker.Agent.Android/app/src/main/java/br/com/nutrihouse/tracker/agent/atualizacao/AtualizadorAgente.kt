package br.com.nutrihouse.tracker.agent.atualizacao

import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageInstaller
import android.os.Build
import android.os.SystemClock
import android.util.Log
import br.com.nutrihouse.tracker.agent.BuildConfig
import br.com.nutrihouse.tracker.agent.dados.AtualizacaoAgente
import br.com.nutrihouse.tracker.agent.dados.BackendClient
import br.com.nutrihouse.tracker.agent.dispositivo.Politica
import java.io.File
import java.security.MessageDigest
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Auto-atualizacao OTA do agente, sem loja: baixa o APK anunciado no heartbeat, confere o
 * SHA-256 e se instala via PackageInstaller.
 *
 * Como a instalacao se resolve depende do privilegio do agente:
 * - **Device Owner**: o commit e silencioso — o aparelho se atualiza sem ninguem tocar nele.
 * - **Sem Device Owner**: o Android exige a confirmacao do usuario (dialogo do sistema). A partir
 *   da primeira atualizacao OTA o agente vira o "installer of record" de si mesmo e, no Android
 *   12+, as seguintes passam a ser silenciosas tambem (USER_ACTION_NOT_REQUIRED).
 *
 * Depois de instalar, o sistema reinicia o app e o BootReceiver (MY_PACKAGE_REPLACED) sobe o
 * servico de novo — o bloqueio persistente sobrevive porque o sinal fica em SharedPreferences.
 */
class AtualizadorAgente(
    private val context: Context,
    private val backend: BackendClient,
) {

    private val politica by lazy { Politica(context) }

    /**
     * Processa a oferta do heartbeat. Silencioso quando nao ha nada a fazer: o heartbeat repete a
     * oferta a cada ciclo ate o aparelho estar na versao, entao qualquer falha se resolve por
     * retentativa natural.
     */
    suspend fun processar(atualizacao: AtualizacaoAgente) {
        // O Android recusa downgrade; versao igual significa "ja atualizei, aguardando o proximo
        // heartbeat reportar". Nos dois casos nao ha nada a fazer.
        if (atualizacao.versaoCode <= BuildConfig.VERSION_CODE) return

        // Uma instalacao por vez: o heartbeat continua chegando enquanto o download anda.
        if (!tentarIniciar()) return

        // So fica de pe depois que a sessao foi entregue ao sistema: qualquer outra saida
        // (sem permissao, hash errado, erro de rede) libera o flag no finally. Antes, os
        // "return" antecipados o deixavam preso ate o processo reiniciar.
        var aguardandoResultado = false
        try {
            if (!podeInstalar()) {
                Log.w(TAG, "Atualizacao ${atualizacao.versaoNome} disponivel, mas falta a permissao de instalar apps.")
                return
            }

            Log.i(TAG, "Atualizando ${BuildConfig.VERSION_NAME} -> ${atualizacao.versaoNome} (code ${atualizacao.versaoCode}).")

            val apk = File(context.filesDir, ARQUIVO_APK)
            backend.baixarApk(apk)

            if (!hashConfere(apk, atualizacao.hashSha256)) {
                // Download corrompido ou pacote trocado no servidor entre o anuncio e o download.
                // Descarta: o proximo heartbeat anuncia o hash atual e o ciclo recomeca limpo.
                Log.w(TAG, "SHA-256 do APK baixado nao confere; descartando.")
                apk.delete()
                return
            }

            instalar(apk)
            // O flag em-andamento fica de pe ate o resultado: o ResultadoInstalacaoReceiver o
            // libera em caso de falha, o sucesso reinicia o processo inteiro de todo jeito, e o
            // timeout de tentarIniciar() cobre o dialogo que o usuario nunca respondeu.
            aguardandoResultado = true
        } catch (e: Exception) {
            Log.w(TAG, "Falha na atualizacao OTA; nova tentativa no proximo heartbeat.", e)
        } finally {
            if (!aguardandoResultado) liberar()
        }
    }

    /**
     * Device Owner instala sem confirmacao. Sem ele, o Android 8+ exige a permissao "instalar
     * apps desconhecidos" concedida na tela de registro; abaixo disso vale a configuracao global
     * de fontes desconhecidas, que nao da para checar por app — deixa o sistema decidir.
     */
    private fun podeInstalar(): Boolean = when {
        politica.ehDeviceOwner -> true
        Build.VERSION.SDK_INT >= Build.VERSION_CODES.O ->
            context.packageManager.canRequestPackageInstalls()
        else -> true
    }

    private fun hashConfere(apk: File, esperadoHex: String): Boolean {
        val digest = MessageDigest.getInstance("SHA-256")
        apk.inputStream().use { entrada ->
            val buffer = ByteArray(64 * 1024)
            while (true) {
                val lidos = entrada.read(buffer)
                if (lidos < 0) break
                digest.update(buffer, 0, lidos)
            }
        }

        val calculado = digest.digest().joinToString("") { "%02x".format(it) }
        return calculado.equals(esperadoHex, ignoreCase = true)
    }

    private fun instalar(apk: File) {
        val installer = context.packageManager.packageInstaller

        // Sessoes que ficaram para tras (dialogo ignorado ate o timeout, processo morto no meio
        // da copia) seguram espaco em disco e podem confundir o sistema: abandona antes da nova.
        abandonarSessoesAntigas(installer)

        val params = PackageInstaller.SessionParams(
            PackageInstaller.SessionParams.MODE_FULL_INSTALL,
        ).apply {
            setAppPackageName(context.packageName)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                // Quando o agente ja e o instalador de si mesmo (toda OTA depois da primeira),
                // o Android 12+ dispensa a confirmacao do usuario.
                setRequireUserAction(PackageInstaller.SessionParams.USER_ACTION_NOT_REQUIRED)
            }
        }

        val sessionId = installer.createSession(params)
        val session = try {
            installer.openSession(sessionId)
        } catch (e: Exception) {
            runCatching { installer.abandonSession(sessionId) }
            throw e
        }

        var enviada = false
        try {
            session.openWrite(ARQUIVO_APK, 0, apk.length()).use { saida ->
                apk.inputStream().use { entrada -> entrada.copyTo(saida) }
                session.fsync(saida)
            }

            // MUTABLE e obrigatorio: o PackageInstaller preenche o resultado nos extras.
            val flags = PendingIntent.FLAG_UPDATE_CURRENT or
                (if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) PendingIntent.FLAG_MUTABLE else 0)

            val intent = Intent(context, ResultadoInstalacaoReceiver::class.java)
                .setAction(ResultadoInstalacaoReceiver.ACAO_RESULTADO)

            val pendente = PendingIntent.getBroadcast(context, sessionId, intent, flags)
            session.commit(pendente.intentSender)
            enviada = true
        } finally {
            // Falha na copia ou no commit: sem abandonar, a sessao ficaria orfa no sistema.
            if (!enviada) runCatching { session.abandon() }
            runCatching { session.close() }
        }

        Log.i(TAG, "Sessao de instalacao $sessionId enviada ao sistema.")
    }

    private fun abandonarSessoesAntigas(installer: PackageInstaller) {
        runCatching {
            installer.mySessions.forEach { antiga ->
                runCatching { installer.abandonSession(antiga.sessionId) }
                    .onSuccess { Log.i(TAG, "Sessao de instalacao antiga ${antiga.sessionId} abandonada.") }
            }
        }.onFailure { Log.w(TAG, "Nao foi possivel listar as sessoes de instalacao antigas.", it) }
    }

    companion object {
        private const val TAG = "AtualizadorAgente"
        private const val ARQUIVO_APK = "atualizacao.apk"

        /**
         * Prazo maximo de uma atualizacao "em andamento". Sem Device Owner, depois do
         * PENDING_USER_ACTION o resultado so chega quando o usuario responde ao dialogo — e se ele
         * o ignorar, o flag ficaria preso e o aparelho nunca mais tentaria atualizar.
         */
        private const val TIMEOUT_EM_ANDAMENTO_MS = 10 * 60 * 1000L

        /**
         * Compartilhado com o ResultadoInstalacaoReceiver: a falha da instalacao chega por
         * broadcast, fora do fluxo que iniciou o processo. Use [tentarIniciar]/[liberar].
         */
        private val emAndamento = AtomicBoolean(false)

        @Volatile
        private var iniciadoEmMs = 0L

        /** Marca o inicio de uma atualizacao; libera sozinho uma tentativa travada ha mais de 10 min. */
        @Synchronized
        internal fun tentarIniciar(): Boolean {
            val agora = SystemClock.elapsedRealtime()
            if (emAndamento.get() && agora - iniciadoEmMs > TIMEOUT_EM_ANDAMENTO_MS) {
                Log.w(TAG, "Atualizacao anterior sem resposta ha mais de 10 min; liberando nova tentativa.")
                emAndamento.set(false)
            }
            if (!emAndamento.compareAndSet(false, true)) return false
            iniciadoEmMs = agora
            return true
        }

        /** Libera o proximo heartbeat para tentar de novo. */
        internal fun liberar() = emAndamento.set(false)
    }
}
