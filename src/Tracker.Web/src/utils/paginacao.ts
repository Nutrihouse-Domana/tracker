/** Paginação de tabelas no cliente: fatia a lista em páginas e monta os props do TablePagination. */
import { useEffect, useMemo, useState } from 'react'

export const TAMANHO_PADRAO_PAGINA = 10
const OPCOES_TAMANHO = [10, 25, 50, 100]

/**
 * Pagina `itens` (lista já filtrada/ordenada) mostrando `tamanhoInicial` por página.
 * Retorna a fatia da página atual e os props prontos para o `<TablePagination />`.
 */
export function usePaginacao<T>(itens: T[] | undefined, tamanhoInicial = TAMANHO_PADRAO_PAGINA) {
  const [pagina, setPagina] = useState(0) // 0-based (TablePagination)
  const [tamanho, setTamanho] = useState(tamanhoInicial)

  const total = itens?.length ?? 0

  // Se a lista encolher (busca/filtro) e a página atual ficar além do fim, recua para a última válida.
  useEffect(() => {
    const ultimaPagina = Math.max(0, Math.ceil(total / tamanho) - 1)
    if (pagina > ultimaPagina) setPagina(ultimaPagina)
  }, [total, tamanho, pagina])

  const itensPagina = useMemo(() => {
    if (!itens) return itens
    const inicio = pagina * tamanho
    return itens.slice(inicio, inicio + tamanho)
  }, [itens, pagina, tamanho])

  const paginacaoProps = {
    component: 'div' as const,
    count: total,
    page: pagina,
    onPageChange: (_: unknown, novaPagina: number) => setPagina(novaPagina),
    rowsPerPage: tamanho,
    onRowsPerPageChange: (e: React.ChangeEvent<HTMLInputElement>) => {
      setTamanho(parseInt(e.target.value, 10))
      setPagina(0)
    },
    rowsPerPageOptions: OPCOES_TAMANHO,
    labelRowsPerPage: 'Por página',
    labelDisplayedRows: ({ from, to, count }: { from: number; to: number; count: number }) =>
      `${from}–${to} de ${count}`,
  }

  return { itensPagina, pagina, tamanho, setPagina, total, paginacaoProps }
}
