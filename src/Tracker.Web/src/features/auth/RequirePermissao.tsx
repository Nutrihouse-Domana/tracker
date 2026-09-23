import type { ReactNode } from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { useMe } from './useAuth'
import { PERMISSOES, primeiraRotaPermitida, type Permissao } from './permissoes'

/**
 * Esconde o conteúdo quando falta a permissão — princípio "ocultar, não desabilitar". Não renderiza
 * nada (nem versão cinza) para quem não pode usar. `fallback` é opcional (padrão: nada).
 */
export function RequirePermissao({
  permissao,
  children,
  fallback = null,
}: {
  permissao: Permissao
  children: ReactNode
  fallback?: ReactNode
}) {
  const { data } = useMe()
  const permitido = data?.permissoes?.includes(permissao) ?? false
  return <>{permitido ? children : fallback}</>
}

/**
 * Guarda de rota: bloqueia acesso direto por URL a uma página sem permissão, redirecionando para a
 * primeira página que o usuário pode ver (nunca deixa cair numa tela negada).
 */
export function PermissaoRoute({ permissao }: { permissao: Permissao }) {
  const { data } = useMe()
  const permissoes = data?.permissoes ?? []
  if (permissoes.includes(permissao)) return <Outlet />
  return <Navigate to={primeiraRotaPermitida(permissoes)} replace />
}

export { PERMISSOES }
