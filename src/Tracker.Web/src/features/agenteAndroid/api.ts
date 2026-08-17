import { apiClient } from '../../api/client'
import type { PacoteAgenteAndroid, SituacaoAgenteAndroid } from '../../api/types'

export interface PublicarPacoteRequest {
  arquivo: File
  versaoCode: number
  versaoNome: string
  senha: string
}

export const agenteAndroidApi = {
  situacao: () =>
    apiClient.get<SituacaoAgenteAndroid>('/api/agente-android/pacotes').then((r) => r.data),

  publicar: ({ arquivo, versaoCode, versaoNome, senha }: PublicarPacoteRequest) => {
    // Multipart: o APK vai junto dos metadados num único POST.
    const form = new FormData()
    form.append('arquivo', arquivo)
    form.append('versaoCode', String(versaoCode))
    form.append('versaoNome', versaoNome)
    form.append('senha', senha)
    return apiClient
      .post<PacoteAgenteAndroid>('/api/agente-android/pacotes', form)
      .then((r) => r.data)
  },
}
