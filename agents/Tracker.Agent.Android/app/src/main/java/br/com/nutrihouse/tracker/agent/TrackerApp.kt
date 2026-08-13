package br.com.nutrihouse.tracker.agent

import android.app.Application
import br.com.nutrihouse.tracker.agent.dados.Armazenamento
import br.com.nutrihouse.tracker.agent.servico.AgenteService
import br.com.nutrihouse.tracker.agent.servico.VigilanteWorker

class TrackerApp : Application() {

    override fun onCreate() {
        super.onCreate()

        // Aparelho ainda nao registrado: quem sobe o servico e a tela de registro, depois de
        // conseguir as permissoes. Subir antes so geraria uma notificacao sem funcao.
        if (Armazenamento(this).registrado) {
            AgenteService.iniciar(this)
        }

        VigilanteWorker.agendar(this)
    }
}
