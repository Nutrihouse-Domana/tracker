import type { Dispositivo } from '../../api/types'

/**
 * Prefixo que a TI usa na identificação dos equipamentos ALUGADOS.
 * A regra é de nomenclatura (não há campo próprio no cadastro): renomear a identificação
 * de um ativo muda a classificação dele aqui. **Exceção: impressoras** — para elas vale o
 * interruptor `alugada` do cadastro, não o prefixo.
 */
export const PREFIXO_ALUGADO = 'MAQ'

type IdentificavelDispositivo = Pick<Dispositivo, 'identificacaoInterna' | 'hostname' | 'numeroSerie'>

/** O que a regra completa de aluguel precisa: identificação + tipo + interruptor da impressora. */
type ClassificavelAluguel = IdentificavelDispositivo & Partial<Pick<Dispositivo, 'tipo' | 'alugada'>>

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

/**
 * Alugado x próprio. Espelho de `ClassificacaoAluguel.EhAlugado(tipo, alugada, identificacao)`:
 * - impressora: vale o interruptor `alugada` (o prefixo é ignorado);
 * - demais tipos: identificação exibida começa com "MAQ" (sem diferenciar maiúsculas).
 */
export function ehAlugado(d: ClassificavelAluguel): boolean {
  if (d.tipo === 'Impressora') return d.alugada === true
  return identificacaoExibida(d).trimStart().toUpperCase().startsWith(PREFIXO_ALUGADO)
}

/** Rótulo para a coluna/chip de aluguel. */
export function rotuloAluguel(d: ClassificavelAluguel): 'Alugado' | 'Próprio' {
  return ehAlugado(d) ? 'Alugado' : 'Próprio'
}
