import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { impressorasApi } from './api'

const KEY = ['impressoras']

/** Último estado; recarrega a cada minuto enquanto a tela está aberta (o ciclo roda a cada 15 min). */
export function useMonitoramentoImpressora(dispositivoId: string, habilitado: boolean) {
  return useQuery({
    queryKey: [...KEY, dispositivoId, 'monitoramento'],
    queryFn: () => impressorasApi.monitoramento(dispositivoId),
    enabled: habilitado && !!dispositivoId,
    refetchInterval: 60_000,
  })
}

/** Leituras e páginas impressas no período (datas "aaaa-mm-dd"). Só consulta com as duas datas. */
export function useLeiturasImpressora(dispositivoId: string, de: string, ate: string, habilitado: boolean) {
  return useQuery({
    queryKey: [...KEY, dispositivoId, 'leituras', { de, ate }],
    queryFn: () => impressorasApi.leituras(dispositivoId, de, ate),
    enabled: habilitado && !!dispositivoId && !!de && !!ate,
  })
}

export function useLerImpressoraAgora(dispositivoId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => impressorasApi.lerAgora(dispositivoId),
    onSuccess: (estado) => {
      queryClient.setQueryData([...KEY, dispositivoId, 'monitoramento'], estado)
      queryClient.invalidateQueries({ queryKey: [...KEY, dispositivoId, 'leituras'] })
      // A leitura pode ter aberto alerta (offline / suprimento baixo) na lista do dispositivo.
      // e mudado online/contador do DispositivoDto (chip da lista e do detalhe).
      queryClient.invalidateQueries({ queryKey: ['dispositivos'] })
    },
  })
}
