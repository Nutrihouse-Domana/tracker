import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { authApi, type LoginRequest } from './api'

const ME_KEY = ['auth', 'me']

export function useMe() {
  return useQuery({
    queryKey: ME_KEY,
    queryFn: authApi.me,
    retry: false,
    staleTime: 5 * 60 * 1000,
  })
}

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: LoginRequest) => authApi.login(data),
    onSuccess: (usuario) => {
      queryClient.setQueryData(ME_KEY, usuario)
    },
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: authApi.logout,
    onSuccess: () => {
      queryClient.setQueryData(ME_KEY, null)
      queryClient.clear()
    },
  })
}
