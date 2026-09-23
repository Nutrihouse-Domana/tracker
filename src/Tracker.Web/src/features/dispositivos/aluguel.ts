import type { Dispositivo } from '../../api/types'

/**
 * Prefixo que a TI usa na identificação dos equipamentos ALUGADOS.
 * A regra é de nomenclatura (não há campo próprio no cadastro): renomear a identificação
 * de um ativo muda a classificação dele aqui.
 */
export const PREFIXO_ALUGADO = 'MAQ'

type IdentificavelDispositivo = Pick<Dispositivo, 'identificacaoInterna' | 'hostname' | 'numeroSerie'>

/** Identificação exibida: rótulo manual da TI, senão hostname, senão número de série. */
export function identificacaoExibida(d: IdentificavelDispositivo): string {
  return d.identificacaoInterna ?? d.hostname ?? d.numeroSerie ?? ''
}

/** Alugado quando a identificação exibida começa com "MAQ" (sem diferenciar maiúsculas). */
export function ehAlugado(d: IdentificavelDispositivo): boolean {
  return identificacaoExibida(d).trimStart().toUpperCase().startsWith(PREFIXO_ALUGADO)
}

/** Rótulo para a coluna/chip de aluguel. */
export function rotuloAluguel(d: IdentificavelDispositivo): 'Alugado' | 'Próprio' {
  return ehAlugado(d) ? 'Alugado' : 'Próprio'
}
