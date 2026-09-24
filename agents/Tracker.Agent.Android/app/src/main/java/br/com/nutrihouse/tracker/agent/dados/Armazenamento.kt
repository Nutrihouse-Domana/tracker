package br.com.nutrihouse.tracker.agent.dados

import android.annotation.SuppressLint
import android.content.Context
import android.content.SharedPreferences
import android.util.Log
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

/**
 * Estado local do agente, em dois arquivos com papeis diferentes:
 *
 * - **Segredos** (token, id, serie, patrimonio): EncryptedSharedPreferences respaldado pelo
 *   Keystore, para o token nao ficar legivel num backup ou num aparelho com root. Se o Keystore
 *   falhar, cai para um arquivo simples — decisao tomada UMA vez e gravada (ver [Preferencias]).
 * - **Estado** (sinal de bloqueio): arquivo simples, sem criptografia de proposito. O sinal nao e
 *   segredo, e amarra-lo ao Keystore faria um aparelho bloqueado voltar LIVRE no dia em que o
 *   Keystore corrompesse (acontece em alguns aparelhos apos atualizacao do sistema).
 *
 * Instanciar e barato: os arquivos sao abertos uma vez por processo e compartilhados.
 *
 * O codigo de instalacao da empresa NAO e guardado aqui de proposito — ele so existe durante a
 * digitacao na tela de registro. Ver BackendClient.registrar.
 */
// commit() de proposito nas escritas criticas (bloqueio, marcadores de migracao): ver [bloqueado].
@SuppressLint("ApplySharedPref")
class Armazenamento(context: Context) {

    private val appContext = context.applicationContext

    private val segredos: SharedPreferences get() = Preferencias.segredos(appContext)

    var token: String?
        get() = segredos.getString(CHAVE_TOKEN, null)
        set(valor) = segredos.edit().putString(CHAVE_TOKEN, valor).apply()

    var dispositivoId: String?
        get() = segredos.getString(CHAVE_DISPOSITIVO_ID, null)
        set(valor) = segredos.edit().putString(CHAVE_DISPOSITIVO_ID, valor).apply()

    /**
     * Sinal de bloqueio persistente — equivalente ao locked.flag do agente Windows. E o que faz o
     * aparelho voltar bloqueado depois de reiniciar, mesmo sem rede e mesmo sem token.
     *
     * Gravado com commit() (sincrono): um apply() perdido num desligamento abrupto devolveria um
     * aparelho bloqueado ao usuario no proximo boot.
     */
    var bloqueado: Boolean
        get() = Preferencias.estado(appContext).getBoolean(CHAVE_BLOQUEADO, false)
        set(valor) {
            Preferencias.estado(appContext).edit().putBoolean(CHAVE_BLOQUEADO, valor).commit()
        }

    /**
     * Numero de serie da etiqueta, digitado pelo tecnico no registro. E a identidade do aparelho
     * no inventario: estavel, ao contrario do ANDROID_ID, que muda quando a assinatura do app
     * muda e por isso ja duplicou registros.
     */
    var numeroSerie: String?
        get() = segredos.getString(CHAVE_NUMERO_SERIE, null)
        set(valor) = segredos.edit().putString(CHAVE_NUMERO_SERIE, valor).apply()

    /** Patrimonio recebido do backend, exibido na tela de bloqueio para a TI identificar o aparelho. */
    var assetTag: String?
        get() = segredos.getString(CHAVE_ASSET_TAG, null)
        set(valor) = segredos.edit().putString(CHAVE_ASSET_TAG, valor).apply()

    val registrado: Boolean get() = !token.isNullOrBlank()

    internal companion object {
        const val CHAVE_TOKEN = "token"
        const val CHAVE_DISPOSITIVO_ID = "dispositivoId"
        const val CHAVE_NUMERO_SERIE = "numeroSerie"
        const val CHAVE_BLOQUEADO = "bloqueado"
        const val CHAVE_ASSET_TAG = "assetTag"
    }
}

/**
 * Abertura dos arquivos de preferencias, uma unica vez por processo.
 *
 * Antes a escolha "criptografado ou simples" era feita a cada `Armazenamento(...)`: se o Keystore
 * falhasse numa instancia e funcionasse na seguinte, o app passava a ter DOIS estados — o token
 * gravado num arquivo e lido no outro, e o sinal de bloqueio podendo sumir. Agora:
 *
 * 1. O arquivo criptografado e tentado algumas vezes, com espera curta (falha transitoria do
 *    Keystore logo apos o boot e comum).
 * 2. Se ainda assim falhar, o marcador [CHAVE_USAR_FALLBACK] e gravado no arquivo de estado e
 *    TODAS as execucoes seguintes usam o arquivo simples — sem alternar de volta.
 * 3. Chaves que versoes antigas deixaram no arquivo simples sao migradas para o escolhido.
 */
@SuppressLint("ApplySharedPref")
internal object Preferencias {

    private const val TAG = "Armazenamento"

    private const val ARQUIVO_SEGREDOS = "tracker-agente"
    private const val ARQUIVO_FALLBACK = "tracker-agente-fallback"
    private const val ARQUIVO_ESTADO = "tracker-agente-estado"

    private const val CHAVE_USAR_FALLBACK = "usarArquivoSimples"
    private const val CHAVE_MIGRACAO_FEITA = "migracaoSegredosV2"

    /** Esperas entre tentativas de abrir o arquivo criptografado. Curtas: pode rodar na main thread. */
    private val ESPERAS_RETENTATIVA_MS = longArrayOf(150L, 400L)

    private val CHAVES_SEGREDO = listOf(
        Armazenamento.CHAVE_TOKEN,
        Armazenamento.CHAVE_DISPOSITIVO_ID,
        Armazenamento.CHAVE_NUMERO_SERIE,
        Armazenamento.CHAVE_ASSET_TAG,
    )

    @Volatile
    private var segredosCache: SharedPreferences? = null

    @Volatile
    private var estadoMigrado = false

    fun segredos(context: Context): SharedPreferences =
        segredosCache ?: synchronized(this) {
            segredosCache ?: abrirSegredos(context).also { segredosCache = it }
        }

    /**
     * Arquivo do sinal de bloqueio. Na primeira abertura depois de atualizar o agente, traz o
     * valor que as versoes antigas guardavam junto dos segredos — em qualquer dos dois arquivos.
     */
    fun estado(context: Context): SharedPreferences {
        val estado = estadoBruto(context)
        if (!estadoMigrado) {
            synchronized(this) {
                if (!estado.contains(Armazenamento.CHAVE_BLOQUEADO)) migrarBloqueado(context, estado)
                estadoMigrado = true
            }
        }
        return estado
    }

    private fun estadoBruto(context: Context): SharedPreferences =
        context.getSharedPreferences(ARQUIVO_ESTADO, Context.MODE_PRIVATE)

    private fun arquivoSimples(context: Context): SharedPreferences =
        context.getSharedPreferences(ARQUIVO_FALLBACK, Context.MODE_PRIVATE)

    /**
     * Bloqueado em QUALQUER lugar = bloqueado. Na duvida, errar para o lado de manter a trava: um
     * falso bloqueio a TI desfaz pelo painel; um falso desbloqueio devolve o aparelho ao usuario.
     */
    private fun migrarBloqueado(context: Context, estado: SharedPreferences) {
        val noSimples = arquivoSimples(context).getBoolean(Armazenamento.CHAVE_BLOQUEADO, false)
        val noEscolhido = runCatching {
            segredos(context).getBoolean(Armazenamento.CHAVE_BLOQUEADO, false)
        }.getOrDefault(false)

        estado.edit().putBoolean(Armazenamento.CHAVE_BLOQUEADO, noSimples || noEscolhido).commit()
    }

    private fun abrirSegredos(context: Context): SharedPreferences {
        val estado = estadoBruto(context)

        val escolhido = if (estado.getBoolean(CHAVE_USAR_FALLBACK, false)) {
            arquivoSimples(context)
        } else {
            abrirCriptografado(context) ?: run {
                // Decisao definitiva: dai em diante todas as instancias e processos usam o simples.
                Log.e(TAG, "Armazenamento criptografado indisponivel; passando a usar o arquivo simples.")
                estado.edit().putBoolean(CHAVE_USAR_FALLBACK, true).commit()
                arquivoSimples(context)
            }
        }

        migrarSegredosDoArquivoSimples(context, estado, escolhido)
        return escolhido
    }

    private fun abrirCriptografado(context: Context): SharedPreferences? {
        for (tentativa in 0..ESPERAS_RETENTATIVA_MS.size) {
            try {
                val chaveMestra = MasterKey.Builder(context)
                    .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
                    .build()

                return EncryptedSharedPreferences.create(
                    context,
                    ARQUIVO_SEGREDOS,
                    chaveMestra,
                    EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                    EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
                )
            } catch (e: Exception) {
                Log.w(TAG, "Falha ao abrir o armazenamento criptografado (tentativa ${tentativa + 1}).", e)
                ESPERAS_RETENTATIVA_MS.getOrNull(tentativa)?.let { Thread.sleep(it) }
            }
        }
        return null
    }

    /**
     * Versoes anteriores decidiam o arquivo por instancia e podem ter deixado o token no arquivo
     * simples mesmo com o Keystore funcionando. Copia o que faltar no arquivo escolhido (sem
     * sobrescrever) e, se o escolhido e o criptografado, apaga os segredos do arquivo simples.
     */
    private fun migrarSegredosDoArquivoSimples(
        context: Context,
        estado: SharedPreferences,
        escolhido: SharedPreferences,
    ) {
        if (estado.getBoolean(CHAVE_MIGRACAO_FEITA, false)) return

        val simples = arquivoSimples(context)
        if (simples !== escolhido) {
            runCatching {
                val edicao = escolhido.edit()
                CHAVES_SEGREDO.forEach { chave ->
                    val valor = simples.getString(chave, null)
                    if (!valor.isNullOrBlank() && escolhido.getString(chave, null).isNullOrBlank()) {
                        edicao.putString(chave, valor)
                    }
                }
                edicao.commit()

                val limpeza = simples.edit()
                CHAVES_SEGREDO.forEach { limpeza.remove(it) }
                limpeza.commit()
            }.onFailure { Log.w(TAG, "Falha ao migrar segredos do arquivo simples.", it) }
        }

        estado.edit().putBoolean(CHAVE_MIGRACAO_FEITA, true).commit()
    }
}
