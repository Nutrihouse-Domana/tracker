import { isAxiosError } from 'axios'
import { apiClient } from '../../api/client'

/**
 * Com `responseType: 'blob'` o corpo de ERRO também chega como Blob, e o `{ message }` do backend
 * ficaria ilegível para o `mensagemErroApi`. Converte o Blob de volta para JSON antes de repassar.
 */
async function erroLegivel(erro: unknown): Promise<unknown> {
  if (isAxiosError(erro) && erro.response?.data instanceof Blob) {
    try {
      erro.response.data = JSON.parse(await erro.response.data.text())
    } catch {
      // Corpo não era JSON (ex.: página de erro do IIS): fica sem mensagem e vale o fallback.
      erro.response.data = undefined
    }
  }
  return erro
}

/** Relatórios oferecidos no menu da conta. O valor é o segmento da rota da API. */
export type TipoRelatorio = 'inventario' | 'aluguel' | 'unidades' | 'termos'
export type FormatoRelatorio = 'pdf' | 'xlsx'

export const relatoriosApi = {
  /**
   * Baixa o arquivo pronto do backend. O nome do arquivo vem no Content-Disposition; se o header
   * não chegar (proxy que o remove), cai num nome local com a data para não salvar "download".
   */
  baixar: async (tipo: TipoRelatorio, formato: FormatoRelatorio) => {
    const resposta = await apiClient
      .get(`/api/relatorios/${tipo}/${formato}`, { responseType: 'blob' })
      .catch(async (erro: unknown) => {
        throw await erroLegivel(erro)
      })
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
