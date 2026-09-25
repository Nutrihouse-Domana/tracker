import type { ConexaoImpressora, TipoImpressora } from '../../api/types'

/** Ordem de exibição nos selects (mesma ordem do enum do backend). */
export const tiposImpressora: TipoImpressora[] = [
  'Laser',
  'JatoDeTinta',
  'Multifuncional',
  'Termica',
  'Matricial',
  'Outra',
]

export const rotuloTipoImpressora: Record<TipoImpressora, string> = {
  Laser: 'Laser',
  JatoDeTinta: 'Jato de tinta',
  Multifuncional: 'Multifuncional',
  Termica: 'Térmica',
  Matricial: 'Matricial',
  Outra: 'Outra',
}

export const rotuloConexao: Record<ConexaoImpressora, string> = {
  Rede: 'Rede',
  Usb: 'USB',
}

/**
 * Checagem leve do endereço (IPv4, IPv6 ou nome de rede), só para avisar no formulário. A regra
 * que vale é a do backend (`FormatoRede.EnderecoValido`), que recusa com 400.
 */
export function enderecoRedePareceValido(valor: string): boolean {
  const v = valor.trim()
  if (!v) return false
  if (/^[\d.]+$/.test(v)) {
    const partes = v.split('.')
    return partes.length === 4 && partes.every((p) => p !== '' && Number(p) <= 255)
  }
  if (v.includes(':')) return /^[0-9a-f:]+$/i.test(v)
  return /^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)*$/i.test(v)
}
