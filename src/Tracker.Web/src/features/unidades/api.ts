import { apiClient } from '../../api/client'
import type { AtualizarUnidadeRequest, CriarUnidadeRequest, Unidade } from '../../api/types'

export const unidadesApi = {
  listar: (incluirInativas = false) =>
    apiClient.get<Unidade[]>('/api/unidades', { params: { incluirInativas } }).then((r) => r.data),
  criar: (data: CriarUnidadeRequest) => apiClient.post<Unidade>('/api/unidades', data).then((r) => r.data),
  atualizar: (id: string, data: AtualizarUnidadeRequest) =>
    apiClient.put(`/api/unidades/${id}`, data).then((r) => r.data),
}
