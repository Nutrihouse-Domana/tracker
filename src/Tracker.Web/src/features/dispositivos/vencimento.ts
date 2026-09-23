/** Quantos dias antes do vencimento o contrato já entra em alerta na interface. */
export const DIAS_DE_ALERTA = 30

export type SituacaoVencimento = 'vencido' | 'proximo' | 'emDia' | 'semData'

/**
 * Dias que faltam para a data (negativo = já passou). Compara só a DATA, zerando a hora dos dois
 * lados: sem isso, "vence hoje" viraria -0,4 dia à tarde e o contrato apareceria como vencido.
 */
export function diasAteVencimento(vencimentoIso: string | null | undefined): number | null {
  if (!vencimentoIso) return null
  const alvo = new Date(vencimentoIso)
  if (Number.isNaN(alvo.getTime())) return null

  const hoje = new Date()
  const umDia = 24 * 60 * 60 * 1000
  const alvoZerado = Date.UTC(alvo.getFullYear(), alvo.getMonth(), alvo.getDate())
  const hojeZerado = Date.UTC(hoje.getFullYear(), hoje.getMonth(), hoje.getDate())
  return Math.round((alvoZerado - hojeZerado) / umDia)
}

export function situacaoVencimento(vencimentoIso: string | null | undefined): SituacaoVencimento {
  const dias = diasAteVencimento(vencimentoIso)
  if (dias === null) return 'semData'
  if (dias < 0) return 'vencido'
  return dias <= DIAS_DE_ALERTA ? 'proximo' : 'emDia'
}

/** Texto curto para o chip: "vencido há 3 dias", "vence hoje", "faltam 12 dias". */
export function rotuloVencimento(vencimentoIso: string | null | undefined): string | null {
  const dias = diasAteVencimento(vencimentoIso)
  if (dias === null) return null
  if (dias === 0) return 'vence hoje'
  if (dias < 0) {
    const passados = Math.abs(dias)
    return `vencido há ${passados} ${passados === 1 ? 'dia' : 'dias'}`
  }
  return `faltam ${dias} ${dias === 1 ? 'dia' : 'dias'}`
}

/** Cor do MUI por situação — vencido pede ação, próximo é aviso, em dia não chama atenção. */
export function corVencimento(situacao: SituacaoVencimento): 'error' | 'warning' | 'default' {
  if (situacao === 'vencido') return 'error'
  if (situacao === 'proximo') return 'warning'
  return 'default'
}

/** Data formatada em pt-BR, sem hora (o backend grava só a data). */
export function formatarVencimento(vencimentoIso: string | null | undefined): string {
  if (!vencimentoIso) return '—'
  const data = new Date(vencimentoIso)
  return Number.isNaN(data.getTime()) ? '—' : data.toLocaleDateString('pt-BR')
}
