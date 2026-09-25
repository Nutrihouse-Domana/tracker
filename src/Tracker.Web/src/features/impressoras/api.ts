import { apiClient } from '../../api/client'

/** Espelho de SuprimentoImpressoraDto. `percentual` nulo = a impressora não informa o nível. */
export interface SuprimentoImpressora {
  descricao: string
  cor: string | null
  percentual: number | null
}

export type ConexaoImpressora = 'Rede' | 'Usb'

/** Espelho de MonitoramentoImpressoraDto (GET /api/impressoras/{id}/monitoramento). */
export interface MonitoramentoImpressora {
  dispositivoId: string
  monitorarSnmp: boolean
  conexao: ConexaoImpressora
  enderecoRede: string | null
  monitoravel: boolean
  ultimaLeituraEm: string | null
  ultimaRespostaEm: string | null
  online: boolean | null
  contadorPaginas: number | null
  estadoReportado: string | null
  erroUltimaLeitura: string | null
  suprimentos: SuprimentoImpressora[]
  limiteSuprimentoPercentual: number
}

export interface LeituraImpressora {
  id: string
  lidaEm: string
  online: boolean
  contadorPaginas: number | null
  estadoReportado: string | null
  suprimentos: SuprimentoImpressora[]
  erro: string | null
}

/** Espelho de LeiturasImpressoraDto. `de`/`ate` são datas civis "aaaa-mm-dd" (DateOnly). */
export interface LeiturasImpressora {
  dispositivoId: string
  de: string
  ate: string
  paginasNoPeriodo: number | null
  contadorInicial: number | null
  contadorFinal: number | null
  totalLeituras: number
  leituras: LeituraImpressora[]
}

export const impressorasApi = {
  monitoramento: (id: string) =>
    apiClient.get<MonitoramentoImpressora>(`/api/impressoras/${id}/monitoramento`).then((r) => r.data),
  leituras: (id: string, de: string, ate: string) =>
    apiClient.get<LeiturasImpressora>(`/api/impressoras/${id}/leituras`, { params: { de, ate } }).then((r) => r.data),
  lerAgora: (id: string) =>
    apiClient.post<MonitoramentoImpressora>(`/api/impressoras/${id}/ler-agora`).then((r) => r.data),
}
