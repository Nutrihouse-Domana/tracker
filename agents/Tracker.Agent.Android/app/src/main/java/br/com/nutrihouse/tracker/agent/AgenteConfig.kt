package br.com.nutrihouse.tracker.agent

/**
 * Constantes de operacao do agente. Os valores de ambiente vem do BuildConfig — inclusive a
 * versao (VERSION_NAME/VERSION_CODE), que e a mesma que o Android compara na atualizacao OTA.
 */
object AgenteConfig {
    /**
     * Cadencia do heartbeat. E a rede de seguranca de entrega de comandos, entao define o pior
     * caso de demora para um bloqueio quando o SignalR esta fora do ar.
     */
    const val INTERVALO_HEARTBEAT_SEGUNDOS = 60L

    /**
     * Verificacao do canal em tempo real. O cliente Java do SignalR **nao** tem reconexao
     * automatica (diferente do cliente .NET usado no agente Windows), entao a reconexao e nossa.
     * Sem isso, uma queda de rede jogava a entrega de comandos para o heartbeat seguinte.
     */
    const val INTERVALO_VERIFICACAO_HUB_SEGUNDOS = 5L

    /**
     * Cadencia do enforcement do bloqueio. Igual a do agente Windows: o usuario destrava a tela
     * e e travado de novo antes de conseguir usar o aparelho.
     */
    const val INTERVALO_ENFORCEMENT_SEGUNDOS = 2L

    /** Espera entre tentativas quando o backend esta inacessivel (rede caiu, aparelho no boot). */
    const val INTERVALO_RETENTATIVA_SEGUNDOS = 30L

    const val CANAL_NOTIFICACAO = "tracker-agente"
    const val ID_NOTIFICACAO_SERVICO = 1001

    const val ROTA_AUTO_ENROLLMENT = "/api/agente/auto-enrollment"
    const val ROTA_HEARTBEAT = "/api/agente/heartbeat"
    const val ROTA_ALERTAS = "/api/agente/alertas"
    const val ROTA_ATUALIZACAO_APK = "/api/agente/atualizacao/apk"
    const val ROTA_HUB_COMANDOS = "/hubs/comandos"

    const val HEADER_TOKEN = "X-Agent-Token"
    const val HEADER_ENROLLMENT_KEY = "X-Enrollment-Key"
}
