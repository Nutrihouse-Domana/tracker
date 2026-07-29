import { apiClient } from '../../api/client'
import type { UsuarioAdm } from '../../api/types'

export interface LoginRequest {
  email: string
  senha: string
}

export const authApi = {
  login: (data: LoginRequest) => apiClient.post<UsuarioAdm>('/api/auth/login', data).then((r) => r.data),
  logout: () => apiClient.post('/api/auth/logout'),
  me: () => apiClient.get<UsuarioAdm>('/api/auth/me').then((r) => r.data),
}
