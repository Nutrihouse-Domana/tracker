/** Quantos dias antes do vencimento o contrato já entra em alerta na interface. */
export const DIAS_DE_ALERTA = 30

export type SituacaoVencimento = 'vencido' | 'proximo' | 'emDia' | 'semData'

/**
 * Converte a data do backend (DateOnly, "2026-10-05") num Date no fuso LOCAL.
 *
 * NÃO use `new Date(texto)` aqui: uma string só-data é interpretada como meia-noite UTC pelo
 * JavaScript e, em UTC-3, `toLocaleDateString` mostraria o DIA ANTERIOR. Por isso o parse é
 * manual. O `slice(0, 10)` também protege contra um valor que venha com hora anexada.
 */
function paraDataLocal(valor: string | null | undefined): Date | null {
  if (!valor) return null
  const [ano, mes, dia] = valor.slice(0, 10).split('-').map(Number)
  if (!ano || !mes || !dia) return null
  const data = new Date(ano, mes - 1, dia)
  return Number.isNaN(data.getTime()) ? null : data
}

/**
 * Dias que faltam para a data (negativo = já passou). Compara só a DATA, zerando a hora dos dois
 * lados: sem isso, "vence hoje" viraria -0,4 dia à tarde e o contrato apareceria como vencido.
 */
export function diasAteVencimento(vencimento: string | null | undefined): number | null {
  const alvo = paraDataLocal(vencimento)
  if (!alvo) return null

  const hoje = new Date()
  const umDia = 24 * 60 * 60 * 1000
  const alvoZerado = Date.UTC(alvo.getFullYear(), alvo.getMonth(), alvo.getDate())
  const hojeZerado = Date.UTC(hoje.getFullYear(), hoje.getMonth(), hoje.getDate())
  return Math.round((alvoZerado - hojeZerado) / umDia)
}

export function situacaoVencimento(vencimento: string | null | undefined): SituacaoVencimento {
  const dias = diasAteVencimento(vencimento)
  if (dias === null) return 'semData'
  if (dias < 0) return 'vencido'
  return dias <= DIAS_DE_ALERTA ? 'proximo' : 'emDia'
}

/** Texto curto para o chip: "vencido há 3 dias", "vence hoje", "faltam 12 dias". */
export function rotuloVencimento(vencimento: string | null | undefined): string | null {
  const dias = diasAteVencimento(vencimento)
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

/** Data em pt-BR (dd/mm/aaaa). */
export function formatarVencimento(vencimento: string | null | undefined): string {
  const data = paraDataLocal(vencimento)
  return data ? data.toLocaleDateString('pt-BR') : '—'
}

/** Valor para o input `type="date"`, que espera exatamente "aaaa-mm-dd". */
export function paraCampoData(vencimento: string | null | undefined): string {
  return vencimento ? vencimento.slice(0, 10) : ''
}
