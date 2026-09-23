import { apiClient } from '../../api/client'

/** Relatórios oferecidos no menu da conta. O valor é o segmento da rota da API. */
export type TipoRelatorio = 'inventario' | 'aluguel' | 'unidades'
export type FormatoRelatorio = 'pdf' | 'xlsx'

export const relatoriosApi = {
  /**
   * Baixa o arquivo pronto do backend. O nome do arquivo vem no Content-Disposition; se o header
   * não chegar (proxy que o remove), cai num nome local com a data para não salvar "download".
   */
  baixar: async (tipo: TipoRelatorio, formato: FormatoRelatorio) => {
    const resposta = await apiClient.get(`/api/relatorios/${tipo}/${formato}`, { responseType: 'blob' })
    const nomeDoHeader = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(
      (resposta.headers['content-disposition'] as string) ?? '',
    )?.[1]
    const nome = nomeDoHeader
      ? decodeURIComponent(nomeDoHeader)
      : `tracker-${tipo}-${new Date().toISOString().slice(0, 10)}.${formato}`

    const url = URL.createObjectURL(resposta.data as Blob)
    const link = document.createElement('a')
    link.href = url
    link.download = nome
    link.click()
    URL.revokeObjectURL(url)
  },
}
