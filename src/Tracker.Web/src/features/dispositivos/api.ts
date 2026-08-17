import { apiClient } from '../../api/client'
import type {
  AlertaDispositivo,
  AtribuirDispositivoRequest,
  ComandoRemoto,
  ComentarioDispositivo,
  CriarComentarioRequest,
  CriarNotebookRequest,
  CriarPerifericoRequest,
  CriarReparoRequest,
  CriarMobileRequest,
  DefinirPosseDispositivoRequest,
  Dispositivo,
  EnrollmentResponse,
  ExcluirDispositivoRequest,
  HistoricoLocalizacao,
  HistoricoMovimentacao,
  HistoricoReparo,
  StatusDispositivo,
  StatusReparo,
  TipoComandoRemoto,
  TipoDispositivo,
} from '../../api/types'

export const dispositivosApi = {
  listar: (filtros?: { tipo?: TipoDispositivo; status?: StatusDispositivo; unidadeId?: string }) =>
    apiClient.get<Dispositivo[]>('/api/dispositivos', { params: filtros }).then((r) => r.data),
  listarArquivados: () =>
    apiClient.get<Dispositivo[]>('/api/dispositivos/arquivados').then((r) => r.data),
  obter: (id: string) => apiClient.get<Dispositivo>(`/api/dispositivos/${id}`).then((r) => r.data),
  criarNotebook: (data: CriarNotebookRequest) =>
    apiClient.post<Dispositivo>('/api/dispositivos/notebooks', data).then((r) => r.data),
  criarMobile: (data: CriarMobileRequest) =>
    apiClient.post<Dispositivo>('/api/dispositivos/mobiles', data).then((r) => r.data),
  criarPeriferico: (data: CriarPerifericoRequest) =>
    apiClient.post<Dispositivo>('/api/dispositivos/perifericos', data).then((r) => r.data),
  atualizarStatus: (id: string, status: StatusDispositivo) =>
    apiClient.put(`/api/dispositivos/${id}/status`, { status }).then((r) => r.data),
  definirUnidade: (id: string, unidadeId: string | null) =>
    apiClient.put(`/api/dispositivos/${id}/unidade`, { unidadeId }).then((r) => r.data),
  definirPosse: (id: string, data: DefinirPosseDispositivoRequest) =>
    apiClient.put(`/api/dispositivos/${id}/posse`, data).then((r) => r.data),
  excluir: (id: string, data: ExcluirDispositivoRequest) =>
    apiClient.post(`/api/dispositivos/${id}/excluir`, data).then((r) => r.data),
  restaurar: (id: string) => apiClient.post(`/api/dispositivos/${id}/restaurar`).then((r) => r.data),
  atribuir: (id: string, data: AtribuirDispositivoRequest) =>
    apiClient.post<HistoricoMovimentacao>(`/api/dispositivos/${id}/atribuir`, data).then((r) => r.data),
  historicoMovimentacao: (id: string) =>
    apiClient.get<HistoricoMovimentacao[]>(`/api/dispositivos/${id}/historico-movimentacao`).then((r) => r.data),
  listarReparos: (id: string) =>
    apiClient.get<HistoricoReparo[]>(`/api/dispositivos/${id}/reparos`).then((r) => r.data),
  criarReparo: (id: string, data: CriarReparoRequest) =>
    apiClient.post<HistoricoReparo>(`/api/dispositivos/${id}/reparos`, data).then((r) => r.data),
  atualizarStatusReparo: (id: string, reparoId: string, status: StatusReparo) =>
    apiClient.put(`/api/dispositivos/${id}/reparos/${reparoId}/status`, { status }).then((r) => r.data),
  listarComentarios: (id: string) =>
    apiClient.get<ComentarioDispositivo[]>(`/api/dispositivos/${id}/comentarios`).then((r) => r.data),
  criarComentario: (id: string, data: CriarComentarioRequest) =>
    apiClient.post<ComentarioDispositivo>(`/api/dispositivos/${id}/comentarios`, data).then((r) => r.data),
  gerarEnrollment: (id: string) =>
    apiClient.post<EnrollmentResponse>(`/api/dispositivos/${id}/enrollment`).then((r) => r.data),
  emitirComando: (id: string, tipoComando: TipoComandoRemoto) =>
    apiClient.post<ComandoRemoto>(`/api/dispositivos/${id}/comandos`, { tipoComando }).then((r) => r.data),
  listarComandos: (id: string) =>
    apiClient.get<ComandoRemoto[]>(`/api/dispositivos/${id}/comandos`).then((r) => r.data),
  historicoLocalizacao: (id: string) =>
    apiClient.get<HistoricoLocalizacao[]>(`/api/dispositivos/${id}/historico-localizacao`).then((r) => r.data),
  listarAlertas: (id: string) =>
    apiClient.get<AlertaDispositivo[]>(`/api/dispositivos/${id}/alertas`).then((r) => r.data),
  reconhecerAlerta: (id: string, alertaId: string) =>
    apiClient.post(`/api/dispositivos/${id}/alertas/${alertaId}/reconhecer`).then((r) => r.data),
}
