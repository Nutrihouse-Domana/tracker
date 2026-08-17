import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { unidadesApi } from './api'
import type { AtualizarUnidadeRequest, CriarUnidadeRequest } from '../../api/types'

const KEY = ['unidades']

export function useUnidades(incluirInativas = false) {
  return useQuery({
    queryKey: [...KEY, { incluirInativas }],
    queryFn: () => unidadesApi.listar(incluirInativas),
  })
}

export function useCriarUnidade() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: CriarUnidadeRequest) => unidadesApi.criar(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useAtualizarUnidade() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: AtualizarUnidadeRequest }) => unidadesApi.atualizar(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KEY })
      // A lista de dispositivos mostra o nome da unidade: renomear precisa refletir lá também.
      queryClient.invalidateQueries({ queryKey: ['dispositivos'] })
    },
  })
}
