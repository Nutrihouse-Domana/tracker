/** Ordenação de tabelas no cliente: hook de estado + comparador estável pt-BR. */
import { useMemo, useState } from 'react'

export type Direcao = 'asc' | 'desc'

/** Compara dois valores tratando null/undefined (vão para o fim) e texto com locale pt-BR. */
function compararValores(a: unknown, b: unknown): number {
  if (a == null && b == null) return 0
  if (a == null) return 1
  if (b == null) return -1
  if (typeof a === 'string' && typeof b === 'string') {
    return a.localeCompare(b, 'pt-BR', { sensitivity: 'base', numeric: true })
  }
  if (a < b) return -1
  if (a > b) return 1
  return 0
}

/**
 * Ordena `itens` por uma das colunas, com alternância asc/desc ao clicar no cabeçalho.
 * Retorna a lista já ordenada e os controles para os `TableSortLabel`.
 */
export function useOrdenacao<T>(
  itens: T[] | undefined,
  colunaInicial: keyof T,
  direcaoInicial: Direcao = 'asc',
) {
  const [coluna, setColuna] = useState<keyof T>(colunaInicial)
  const [direcao, setDirecao] = useState<Direcao>(direcaoInicial)

  /** Clica no cabeçalho: inverte a direção se for a mesma coluna, senão ordena asc pela nova. */
  const ordenarPor = (nova: keyof T) => {
    if (nova === coluna) {
      setDirecao((d) => (d === 'asc' ? 'desc' : 'asc'))
    } else {
      setColuna(nova)
      setDirecao('asc')
    }
  }

  const ordenados = useMemo(() => {
    if (!itens) return itens
    const fator = direcao === 'asc' ? 1 : -1
    return [...itens].sort((a, b) => fator * compararValores(a[coluna], b[coluna]))
  }, [itens, coluna, direcao])

  return { ordenados, coluna, direcao, ordenarPor }
}
