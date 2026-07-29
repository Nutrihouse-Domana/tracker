import { useQuery } from '@tanstack/react-query'
import { dashboardApi } from './api'

export function useEstatisticasDashboard() {
  return useQuery({
    queryKey: ['dashboard', 'estatisticas'],
    queryFn: dashboardApi.estatisticas,
  })
}
