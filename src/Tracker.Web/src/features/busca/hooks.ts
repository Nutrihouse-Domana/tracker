import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { buscaApi } from './api'

/** Atrasa a propagação do valor para não disparar uma requisição por tecla digitada. */
export function useDebounce<T>(valor: T, atrasoMs = 300) {
  const [atrasado, setAtrasado] = useState(valor)

  useEffect(() => {
    const timer = setTimeout(() => setAtrasado(valor), atrasoMs)
    return () => clearTimeout(timer)
  }, [valor, atrasoMs])

  return atrasado
}

/** Busca global com debounce; só consulta a API a partir de 2 caracteres. */
export function useBuscaGlobal(termo: string, limite = 8) {
  const termoDebounced = useDebounce(termo.trim(), 300)
  const habilitado = termoDebounced.length >= 2

  const query = useQuery({
    queryKey: ['busca', termoDebounced, limite],
    queryFn: () => buscaApi.buscar(termoDebounced, limite),
    enabled: habilitado,
    staleTime: 30_000,
  })

  return { ...query, habilitado, termoDebounced }
}
