package br.com.nutrihouse.tracker.agent.dispositivo

import android.app.admin.DevicePolicyManager
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.UserManager
import android.provider.Settings
import android.util.Log
import br.com.nutrihouse.tracker.agent.R
import br.com.nutrihouse.tracker.agent.admin.TrackerDeviceAdminReceiver

/** Espelha Tracker.Domain.Enums.NivelProtecaoAgente. A ordem importa: maior = mais protegido. */
enum class NivelProtecao { Nenhuma, DeviceAdmin, DeviceOwner }

/**
 * Fachada sobre o DevicePolicyManager.
 *
 * Duas realidades bem diferentes:
 * - **Device Admin** (APK instalado num aparelho em uso): da acesso a lockNow(). O Android impede
 *   a desinstalacao enquanto o admin estiver ativo, mas o usuario pode desativa-lo nas
 *   Configuracoes — e por isso que TrackerDeviceAdminReceiver avisa o backend quando isso ocorre.
 * - **Device Owner** (aparelho zerado, provisionado por QR): permite bloquear a desinstalacao de
 *   fato, proibir restauracao de fabrica e usar modo quiosque na tela de bloqueio.
 */
class Politica(private val context: Context) {

    private val dpm: DevicePolicyManager
        get() = context.getSystemService(Context.DEVICE_POLICY_SERVICE) as DevicePolicyManager

    val admin: ComponentName get() = ComponentName(context, TrackerDeviceAdminReceiver::class.java)

    val ehAdminAtivo: Boolean get() = runCatching { dpm.isAdminActive(admin) }.getOrDefault(false)

    val ehDeviceOwner: Boolean
        get() = runCatching { dpm.isDeviceOwnerApp(context.packageName) }.getOrDefault(false)

    val nivelProtecao: NivelProtecao
        get() = when {
            ehDeviceOwner -> NivelProtecao.DeviceOwner
            ehAdminAtivo -> NivelProtecao.DeviceAdmin
            else -> NivelProtecao.Nenhuma
        }

    /** True quando o agente pode desenhar sobre outros apps — requisito da tela de bloqueio. */
    val podeSobreporJanelas: Boolean get() = Settings.canDrawOverlays(context)

    /** Intent da tela do sistema que pede a permissao de administrador ao usuario. */
    fun intentParaAtivarAdmin(): Intent =
        Intent(DevicePolicyManager.ACTION_ADD_DEVICE_ADMIN).apply {
            putExtra(DevicePolicyManager.EXTRA_DEVICE_ADMIN, admin)
            putExtra(
                DevicePolicyManager.EXTRA_ADD_EXPLANATION,
                context.getString(R.string.enrollment_admin_texto),
            )
        }

    /** Trava a tela imediatamente. Exige Device Admin ativo; sem ele nao ha nada a fazer. */
    fun travarTela(): Boolean {
        if (!ehAdminAtivo) return false

        return runCatching { dpm.lockNow(); true }
            .onFailure { Log.w(TAG, "lockNow falhou", it) }
            .getOrDefault(false)
    }

    /**
     * Restricoes que so o Device Owner consegue aplicar. Chamado a cada inicializacao do servico
     * porque uma atualizacao do sistema pode limpar parte delas.
     */
    fun aplicarEndurecimentoDeviceOwner() {
        if (!ehDeviceOwner) return

        runCatching {
            dpm.setUninstallBlocked(admin, context.packageName, true)
            // Sem estas duas o usuario contornaria o agente restaurando o aparelho.
            dpm.addUserRestriction(admin, UserManager.DISALLOW_FACTORY_RESET)
            dpm.addUserRestriction(admin, UserManager.DISALLOW_SAFE_BOOT)
            // Autoriza a propria tela de bloqueio a entrar em modo quiosque.
            dpm.setLockTaskPackages(admin, arrayOf(context.packageName))
        }.onFailure { Log.w(TAG, "Falha ao aplicar endurecimento de Device Owner", it) }
    }

    /**
     * Desfaz o endurecimento. Necessario antes de descomissionar o aparelho, senao a TI nao
     * consegue nem restaurar nem remover o agente.
     */
    fun removerEndurecimentoDeviceOwner() {
        if (!ehDeviceOwner) return

        runCatching {
            dpm.setUninstallBlocked(admin, context.packageName, false)
            dpm.clearUserRestriction(admin, UserManager.DISALLOW_FACTORY_RESET)
            dpm.clearUserRestriction(admin, UserManager.DISALLOW_SAFE_BOOT)
        }.onFailure { Log.w(TAG, "Falha ao remover endurecimento de Device Owner", it) }
    }

    /**
     * Impede que o usuario troque de app enquanto a tela de bloqueio estiver aberta. Sem Device
     * Owner nao existe equivalente: a mitigacao e reaplicar lockNow() em ciclo curto.
     */
    fun definirQuiosque(ativo: Boolean) {
        if (!ehDeviceOwner || Build.VERSION.SDK_INT < Build.VERSION_CODES.M) return

        runCatching {
            dpm.setLockTaskPackages(admin, if (ativo) arrayOf(context.packageName) else emptyArray())
        }.onFailure { Log.w(TAG, "Falha ao configurar quiosque", it) }
    }

    private companion object {
        const val TAG = "Politica"
    }
}
