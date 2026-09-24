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
        // EXCECAO: aparelho bloqueado sobe o servico mesmo sem token (um 401 o apaga) — e o laço
        // de enforcement do servico que sustenta a trava.
        val armazenamento = Armazenamento(this)
        if (armazenamento.registrado || armazenamento.bloqueado) {
            AgenteService.iniciar(this)
        }

        VigilanteWorker.agendar(this)
    }
}
