import type { Dispositivo } from '../../api/types'

/**
 * Prefixo que a TI usa na identificação dos equipamentos ALUGADOS.
 * A regra é de nomenclatura (não há campo próprio no cadastro): renomear a identificação
 * de um ativo muda a classificação dele aqui.
 */
export const PREFIXO_ALUGADO = 'MAQ'

type IdentificavelDispositivo = Pick<Dispositivo, 'identificacaoInterna' | 'hostname' | 'numeroSerie'>

/** Trata nulo, vazio e só-espaços como ausente (igual ao `IsNullOrWhiteSpace` do C#). */
function preenchido(valor: string | null | undefined): valor is string {
  return valor != null && valor.trim() !== ''
}

/**
 * Identificação exibida: rótulo manual da TI, senão hostname, senão número de série.
 * Espelho de `ClassificacaoAluguel.IdentificacaoExibida` (backend): string vazia ou só com
 * espaços é pulada, senão a coluna da tela e o relatório discordariam sobre o mesmo aparelho.
 */
export function identificacaoExibida(d: IdentificavelDispositivo): string {
  if (preenchido(d.identificacaoInterna)) return d.identificacaoInterna
  if (preenchido(d.hostname)) return d.hostname
  return d.numeroSerie ?? ''
}

/** Alugado quando a identificação exibida começa com "MAQ" (sem diferenciar maiúsculas). */
export function ehAlugado(d: IdentificavelDispositivo): boolean {
  return identificacaoExibida(d).trimStart().toUpperCase().startsWith(PREFIXO_ALUGADO)
}

/** Rótulo para a coluna/chip de aluguel. */
export function rotuloAluguel(d: IdentificavelDispositivo): 'Alugado' | 'Próprio' {
  return ehAlugado(d) ? 'Alugado' : 'Próprio'
}
