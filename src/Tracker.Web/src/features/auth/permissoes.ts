import { useMe } from './useAuth'

/** Espelho do catálogo de permissões do backend (Tracker.Application/Seguranca/Permissoes.cs). */
export const PERMISSOES = {
  acessosGerir: 'acessos.gerir',
  dispositivosVer: 'dispositivos.ver',
  dispositivosCadastrar: 'dispositivos.cadastrar',
  dispositivosMovimentar: 'dispositivos.movimentar',
  dispositivosExcluir: 'dispositivos.excluir',
  dispositivosExcluirDefinitivo: 'dispositivos.excluir_definitivo',
  dispositivosBloquear: 'dispositivos.bloquear',
  agentePublicar: 'agente.publicar',
  usuariosGerir: 'usuarios.gerir',
  unidadesGerir: 'unidades.gerir',
  auditoriaVer: 'auditoria.ver',
} as const

export type Permissao = (typeof PERMISSOES)[keyof typeof PERMISSOES]

/** True se o usuário logado tem a permissão. Enquanto o /me carrega, devolve false. */
export function usePermissao(permissao: Permissao): boolean {
  const { data } = useMe()
  return data?.permissoes?.includes(permissao) ?? false
}

/** Retorna uma função que checa qualquer permissão sem re-consultar o /me a cada chamada. */
export function usePermissoes() {
  const { data } = useMe()
  const permissoes = data?.permissoes ?? []
  return {
    tem: (p: Permissao) => permissoes.includes(p),
    temAlguma: (...ps: Permissao[]) => ps.some((p) => permissoes.includes(p)),
    lista: permissoes,
  }
}

/**
 * Primeira rota que o usuário tem permissão de ver, em ordem de preferência. Serve para a rota-raiz
 * e para redirecionar quem tenta abrir por URL uma página sem acesso — ninguém aterrissa numa tela
 * negada. '/sem-acesso' é o último recurso (usuário sem nenhuma permissão de navegação).
 */
export function primeiraRotaPermitida(permissoes: string[]): string {
  const tem = (p: string) => permissoes.includes(p)
  if (tem(PERMISSOES.dispositivosVer)) return '/'
  if (tem(PERMISSOES.usuariosGerir)) return '/usuarios'
  if (tem(PERMISSOES.unidadesGerir)) return '/unidades'
  if (tem(PERMISSOES.agentePublicar)) return '/agente-android'
  if (tem(PERMISSOES.auditoriaVer)) return '/auditoria'
  if (tem(PERMISSOES.acessosGerir)) return '/acessos'
  return '/sem-acesso'
}
