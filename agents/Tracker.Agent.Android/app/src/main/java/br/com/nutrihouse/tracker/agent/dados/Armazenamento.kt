package br.com.nutrihouse.tracker.agent.dados

import android.content.Context
import android.content.SharedPreferences
import android.util.Log
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

/**
 * Estado local do agente. O token do dispositivo fica em EncryptedSharedPreferences (respaldado
 * pelo Keystore do aparelho), para nao ficar legivel num backup ou num aparelho com root.
 *
 * O codigo de instalacao da empresa NAO e guardado aqui de proposito — ele so existe durante a
 * digitacao na tela de registro. Ver BackendClient.registrar.
 */
class Armazenamento(context: Context) {

    private val prefs: SharedPreferences = criarPrefs(context)

    var token: String?
        get() = prefs.getString(CHAVE_TOKEN, null)
        set(valor) = prefs.edit().putString(CHAVE_TOKEN, valor).apply()

    var dispositivoId: String?
        get() = prefs.getString(CHAVE_DISPOSITIVO_ID, null)
        set(valor) = prefs.edit().putString(CHAVE_DISPOSITIVO_ID, valor).apply()

    /**
     * Sinal de bloqueio persistente — equivalente ao locked.flag do agente Windows. E o que faz o
     * aparelho voltar bloqueado depois de reiniciar, mesmo sem rede.
     */
    var bloqueado: Boolean
        get() = prefs.getBoolean(CHAVE_BLOQUEADO, false)
        set(valor) = prefs.edit().putBoolean(CHAVE_BLOQUEADO, valor).apply()

    /** Patrimonio recebido do backend, exibido na tela de bloqueio para a TI identificar o aparelho. */
    var assetTag: String?
        get() = prefs.getString(CHAVE_ASSET_TAG, null)
        set(valor) = prefs.edit().putString(CHAVE_ASSET_TAG, valor).apply()

    val registrado: Boolean get() = !token.isNullOrBlank()

    private companion object {
        const val ARQUIVO = "tracker-agente"
        const val CHAVE_TOKEN = "token"
        const val CHAVE_DISPOSITIVO_ID = "dispositivoId"
        const val CHAVE_BLOQUEADO = "bloqueado"
        const val CHAVE_ASSET_TAG = "assetTag"

        fun criarPrefs(context: Context): SharedPreferences = try {
            val chaveMestra = MasterKey.Builder(context)
                .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
                .build()

            EncryptedSharedPreferences.create(
                context,
                ARQUIVO,
                chaveMestra,
                EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
            )
        } catch (e: Exception) {
            // Keystore corrompido (acontece em alguns aparelhos apos atualizacao do sistema).
            // Perder o token e recuperavel — o agente se registra de novo; ficar sem armazenamento
            // deixaria o aparelho invisivel para a TI, que e pior.
            Log.e("Armazenamento", "Falha no armazenamento criptografado; usando prefs simples.", e)
            context.getSharedPreferences("$ARQUIVO-fallback", Context.MODE_PRIVATE)
        }
    }
}
