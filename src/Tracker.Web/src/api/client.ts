import axios from 'axios'
import { queryClient } from './queryClient'

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5171',
  withCredentials: true,
})

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      queryClient.setQueryData(['auth', 'me'], null)
    }
    return Promise.reject(error)
  },
)
