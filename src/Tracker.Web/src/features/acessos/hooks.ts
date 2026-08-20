import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { acessosApi } from './api'
import type {
  AlterarAtivoAcessoRequest,
  AtualizarAcessoRequest,
  CriarAcessoRequest,
  RedefinirSenhaAcessoRequest,
} from '../../api/types'

const KEY = ['acessos']
// Alterar acessos pode mudar as permissões do PRÓPRIO usuário logado: invalidar o /me faz os
// menus/rotas dele refletirem na hora (sem esperar o staleTime nem exigir relogar).
const ME_KEY = ['auth', 'me']

export function useAcessos() {
  return useQuery({ queryKey: KEY, queryFn: acessosApi.listar })
}

export function useCatalogoPermissoes() {
  return useQuery({
    queryKey: ['acessos', 'permissoes'],
    queryFn: acessosApi.permissoes,
    staleTime: 60 * 60 * 1000, // catálogo é praticamente estático
  })
}

export function useCriarAcesso() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: CriarAcessoRequest) => acessosApi.criar(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KEY })
      queryClient.invalidateQueries({ queryKey: ME_KEY })
    },
  })
}

export function useAtualizarAcesso() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: AtualizarAcessoRequest }) => acessosApi.atualizar(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KEY })
      queryClient.invalidateQueries({ queryKey: ME_KEY })
    },
  })
}

export function useAlterarAtivoAcesso() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: AlterarAtivoAcessoRequest }) => acessosApi.alterarAtivo(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KEY })
      queryClient.invalidateQueries({ queryKey: ME_KEY })
    },
  })
}

export function useRedefinirSenhaAcesso() {
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: RedefinirSenhaAcessoRequest }) =>
      acessosApi.redefinirSenha(id, data),
  })
}
