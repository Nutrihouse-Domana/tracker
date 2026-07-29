import { apiClient } from '../../api/client'
import type { EstatisticasDashboard } from '../../api/types'

export const dashboardApi = {
  estatisticas: () => apiClient.get<EstatisticasDashboard>('/api/dashboard/estatisticas').then((r) => r.data),
}
