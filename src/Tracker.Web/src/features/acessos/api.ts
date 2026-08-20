import { apiClient } from '../../api/client'
import type {
  Acesso,
  AlterarAtivoAcessoRequest,
  AtualizarAcessoRequest,
  CriarAcessoRequest,
  Permissao,
  RedefinirSenhaAcessoRequest,
} from '../../api/types'

export const acessosApi = {
  listar: () => apiClient.get<Acesso[]>('/api/acessos').then((r) => r.data),
  permissoes: () => apiClient.get<Permissao[]>('/api/acessos/permissoes').then((r) => r.data),
  criar: (data: CriarAcessoRequest) => apiClient.post<Acesso>('/api/acessos', data).then((r) => r.data),
  atualizar: (id: string, data: AtualizarAcessoRequest) =>
    apiClient.put(`/api/acessos/${id}`, data).then((r) => r.data),
  alterarAtivo: (id: string, data: AlterarAtivoAcessoRequest) =>
    apiClient.put(`/api/acessos/${id}/ativo`, data).then((r) => r.data),
  redefinirSenha: (id: string, data: RedefinirSenhaAcessoRequest) =>
    apiClient.post(`/api/acessos/${id}/redefinir-senha`, data).then((r) => r.data),
}
