import { useMutation } from '@tanstack/react-query'
import { relatoriosApi, type FormatoRelatorio, type TipoRelatorio } from './api'

/**
 * Download de um relatório. É mutation (e não query) de propósito: o arquivo é gerado sob demanda
 * a cada clique e não deve ficar em cache — o parque muda e o relatório é uma foto do momento.
 */
export function useBaixarRelatorio() {
  return useMutation({
    mutationFn: ({ tipo, formato }: { tipo: TipoRelatorio; formato: FormatoRelatorio }) =>
      relatoriosApi.baixar(tipo, formato),
  })
}
