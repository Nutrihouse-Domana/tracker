import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { usuariosApi } from './api'
import type { AtualizarUsuarioRequest, CriarUsuarioRequest, Usuario } from '../../api/types'

const KEY = ['usuarios']

export function useUsuarios(incluirInativos = false) {
  return useQuery({
    queryKey: [...KEY, { incluirInativos }],
    queryFn: () => usuariosApi.listar(incluirInativos),
  })
}

export function useCriarUsuario() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: CriarUsuarioRequest) => usuariosApi.criar(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useAtualizarUsuario() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: AtualizarUsuarioRequest }) => usuariosApi.atualizar(id, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useDesativarUsuario() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => usuariosApi.desativar(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

/**
 * Reativa um detentor desativado. Não há endpoint próprio: é o mesmo PUT da edição com
 * `ativo = true`, reenviando os dados atuais (o backend audita como edição).
 */
export function useReativarUsuario() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (u: Usuario) =>
      usuariosApi.atualizar(u.id, { nome: u.nome, email: u.email, telefone: u.telefone, ativo: true }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}
