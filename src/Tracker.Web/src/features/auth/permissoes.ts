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

/** Uma permissão ou uma lista delas que precisam estar TODAS presentes. */
export type ExigenciaPermissao = Permissao | readonly Permissao[]

/**
 * Cadastros de Usuários e Unidades: a tela lista pelos GETs, que no backend pedem
 * `dispositivos.ver` (alimentam formulários do inventário). Só a gestão não basta — sem
 * `dispositivos.ver` a página abriria com 403 e tabela vazia.
 */
export const EXIGENCIA_USUARIOS: ExigenciaPermissao = [PERMISSOES.usuariosGerir, PERMISSOES.dispositivosVer]
export const EXIGENCIA_UNIDADES: ExigenciaPermissao = [PERMISSOES.unidadesGerir, PERMISSOES.dispositivosVer]

/** True se `permissoes` atende a exigência (todas as permissões, quando for lista). */
export function atendeExigencia(permissoes: readonly string[], exigencia: ExigenciaPermissao): boolean {
  const lista: readonly Permissao[] = typeof exigencia === 'string' ? [exigencia] : exigencia
  return lista.every((p) => permissoes.includes(p))
}

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
  const tem = (p: ExigenciaPermissao) => atendeExigencia(permissoes, p)
  if (tem(PERMISSOES.dispositivosVer)) return '/'
  if (tem(EXIGENCIA_USUARIOS)) return '/usuarios'
  if (tem(EXIGENCIA_UNIDADES)) return '/unidades'
  if (tem(PERMISSOES.agentePublicar)) return '/agente-android'
  if (tem(PERMISSOES.auditoriaVer)) return '/auditoria'
  if (tem(PERMISSOES.acessosGerir)) return '/acessos'
  return '/sem-acesso'
}
