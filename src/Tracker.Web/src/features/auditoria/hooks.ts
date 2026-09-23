import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { auditoriaApi, type FiltrosAuditoria } from './api'

export function useAuditoria(filtros: FiltrosAuditoria) {
  return useQuery({
    queryKey: ['auditoria', filtros],
    queryFn: () => auditoriaApi.listar(filtros),
    // Mantém a página atual visível enquanto a próxima carrega (paginação sem "piscar").
    placeholderData: keepPreviousData,
  })
}

export function useAcoesAuditoria() {
  return useQuery({
    queryKey: ['auditoria', 'acoes'],
    queryFn: auditoriaApi.acoes,
    staleTime: 60 * 60 * 1000,
  })
}
