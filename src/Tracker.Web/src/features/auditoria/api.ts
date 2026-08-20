import { apiClient } from '../../api/client'
import type { AcaoAuditoriaOpcao, PaginaAuditoria } from '../../api/types'

export interface FiltrosAuditoria {
  acao?: string
  de?: string
  ate?: string
  q?: string
  entidadeId?: string
  pagina?: number
  tamanho?: number
}

export const auditoriaApi = {
  listar: (filtros: FiltrosAuditoria) =>
    apiClient
      .get<PaginaAuditoria>('/api/auditoria', {
        // Só envia os filtros preenchidos (evita ?acao=&de=... na URL).
        params: Object.fromEntries(Object.entries(filtros).filter(([, v]) => v !== undefined && v !== '')),
      })
      .then((r) => r.data),
  acoes: () => apiClient.get<AcaoAuditoriaOpcao[]>('/api/auditoria/acoes').then((r) => r.data),
}
