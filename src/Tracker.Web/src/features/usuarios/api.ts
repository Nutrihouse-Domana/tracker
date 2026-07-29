import { apiClient } from '../../api/client'
import type { AtualizarUsuarioRequest, CriarUsuarioRequest, Usuario } from '../../api/types'

export const usuariosApi = {
  listar: (incluirInativos = false) =>
    apiClient
      .get<Usuario[]>('/api/usuarios', { params: { incluirInativos } })
      .then((r) => r.data),
  obter: (id: string) => apiClient.get<Usuario>(`/api/usuarios/${id}`).then((r) => r.data),
  criar: (data: CriarUsuarioRequest) => apiClient.post<Usuario>('/api/usuarios', data).then((r) => r.data),
  atualizar: (id: string, data: AtualizarUsuarioRequest) =>
    apiClient.put(`/api/usuarios/${id}`, data).then((r) => r.data),
  desativar: (id: string) => apiClient.delete(`/api/usuarios/${id}`).then((r) => r.data),
}
