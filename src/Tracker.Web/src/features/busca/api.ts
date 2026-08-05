import { apiClient } from '../../api/client'
import type { BuscaGlobal } from '../../api/types'

export const buscaApi = {
  /** Busca global (dispositivos + usuários) usada pela barra de busca do topo. */
  buscar: (termo: string, limite = 8) =>
    apiClient.get<BuscaGlobal>('/api/busca', { params: { q: termo, limite } }).then((r) => r.data),
}
