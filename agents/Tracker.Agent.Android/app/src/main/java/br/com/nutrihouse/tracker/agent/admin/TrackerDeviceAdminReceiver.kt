package br.com.nutrihouse.tracker.agent.admin

import android.app.admin.DeviceAdminReceiver
import android.content.Context
import android.content.Intent
import android.util.Log
import br.com.nutrihouse.tracker.agent.R
import br.com.nutrihouse.tracker.agent.dados.Armazenamento
import br.com.nutrihouse.tracker.agent.dados.BackendClient
import br.com.nutrihouse.tracker.agent.dados.TipoAlerta
import br.com.nutrihouse.tracker.agent.dispositivo.Politica
import br.com.nutrihouse.tracker.agent.servico.AgenteService
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

/**
 * Recebe os eventos de administracao do aparelho.
 *
 * O papel principal aqui e **detectar a perda de protecao**. Sem Device Owner o usuario pode
 * desativar o administrador nas Configuracoes e, com isso, desinstalar o agente. Nao ha como
 * impedir — mas ha como avisar: `onDisableRequested` mostra o aviso e `onDisabled` reporta o
 * evento ao backend, que marca o aparelho como desprotegido para a TI agir.
 */
class TrackerDeviceAdminReceiver : DeviceAdminReceiver() {

    override fun onEnabled(context: Context, intent: Intent) {
        Log.i(TAG, "Administrador ativado.")
        Politica(context).aplicarEndurecimentoDeviceOwner()
        AgenteService.iniciar(context)
    }

    /**
     * Texto exibido na tela de confirmacao do sistema quando o usuario tenta desativar o admin.
     * Aproveitamos para reportar a tentativa: se ele confirmar, o aparelho pode sair da rede antes
     * de conseguirmos avisar.
     */
    override fun onDisableRequested(context: Context, intent: Intent): CharSequence {
        reportar(context, TipoAlerta.AdminDesativado, "Usuário abriu a confirmação de desativação do administrador.")
        return context.getString(R.string.admin_aviso_desativacao)
    }

    override fun onDisabled(context: Context, intent: Intent) {
        Log.w(TAG, "Administrador DESATIVADO — aparelho sem protecao.")
        reportar(context, TipoAlerta.AdminDesativado, "Permissão de administrador desativada no aparelho.")
    }

    /** Chamado ao final do provisionamento por QR: e aqui que o aparelho vira Device Owner. */
    override fun onProfileProvisioningComplete(context: Context, intent: Intent) {
        Log.i(TAG, "Provisionamento concluido; aplicando endurecimento de Device Owner.")
        Politica(context).aplicarEndurecimentoDeviceOwner()
        AgenteService.iniciar(context)
    }

    /**
     * Dispara o alerta sem bloquear o receiver. Se a rede estiver fora o alerta se perde, e a TI
     * ainda percebe pelo heartbeat: o proximo ciclo reporta NivelProtecao = Nenhuma.
     */
    private fun reportar(context: Context, tipo: TipoAlerta, detalhe: String) {
        val appContext = context.applicationContext
        val armazenamento = Armazenamento(appContext)
        if (!armazenamento.registrado) return

        escopo.launch {
            runCatching { BackendClient(appContext, armazenamento).reportarAlerta(tipo, detalhe) }
                .onFailure { Log.w(TAG, "Nao foi possivel reportar o alerta agora.", it) }
        }
    }

    private companion object {
        const val TAG = "TrackerAdmin"

        /** SupervisorJob: uma falha de rede num alerta nao pode cancelar os proximos. */
        val escopo = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    }
}
