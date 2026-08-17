import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { agenteAndroidApi, type PublicarPacoteRequest } from './api'

const KEY = ['agente-android']

export function useSituacaoAgenteAndroid() {
  return useQuery({ queryKey: KEY, queryFn: agenteAndroidApi.situacao })
}

export function usePublicarPacote() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: PublicarPacoteRequest) => agenteAndroidApi.publicar(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}
