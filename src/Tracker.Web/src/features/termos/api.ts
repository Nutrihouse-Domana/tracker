import { apiClient } from '../../api/client'
import type { TermoDeUso } from '../../api/types'

export const termosApi = {
  gerarPdf: (data: {
    dispositivoId: string
    usuarioId: string
    versaoTermo: string
    estadoDispositivo?: string
  }) =>
    apiClient
      .post('/api/termosdeuso/gerar-pdf', data, { responseType: 'blob' })
      .then((r) => r.data as Blob),
  upload: (data: {
    dispositivoId: string
    usuarioId: string
    versaoTermo: string
    arquivo: File
  }) => {
    const form = new FormData()
    form.append('dispositivoId', data.dispositivoId)
    form.append('usuarioId', data.usuarioId)
    form.append('versaoTermo', data.versaoTermo)
    // A autoria (quem registrou) é resolvida no servidor pelo ADM logado.
    form.append('arquivo', data.arquivo)
    return apiClient.post<TermoDeUso>('/api/termosdeuso', form).then((r) => r.data)
  },
  listarPorDispositivo: (dispositivoId: string) =>
    apiClient.get<TermoDeUso[]>(`/api/termosdeuso/dispositivo/${dispositivoId}`).then((r) => r.data),
  visualizar: (termoId: string) =>
    apiClient.get(`/api/termosdeuso/${termoId}/visualizar`, { responseType: 'blob' }).then((r) => r.data as Blob),
}

export function baixarPdfComoArquivo(blob: Blob, nomeArquivo: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = nomeArquivo
  link.click()
  URL.revokeObjectURL(url)
}

export function abrirPdfEmNovaAba(blob: Blob) {
  const url = URL.createObjectURL(blob)
  window.open(url, '_blank')
  // não revoga a URL imediatamente — a aba nova precisa dela para renderizar o PDF.
}
