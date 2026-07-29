import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { termosApi } from './api'

export function useTermosPorDispositivo(dispositivoId: string | undefined) {
  return useQuery({
    queryKey: ['termos', dispositivoId],
    queryFn: () => termosApi.listarPorDispositivo(dispositivoId!),
    enabled: !!dispositivoId,
  })
}

export function useGerarPdfTermo() {
  return useMutation({ mutationFn: termosApi.gerarPdf })
}

export function useUploadTermo() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: termosApi.upload,
    onSuccess: (_data, variables) =>
      queryClient.invalidateQueries({ queryKey: ['termos', variables.dispositivoId] }),
  })
}

export function useVisualizarTermo() {
  return useMutation({ mutationFn: termosApi.visualizar })
}
