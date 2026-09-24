import type { StatusDispositivo } from '../../api/types'

/** Rótulo legível (pt-BR) para o status do dispositivo — evita exibir o enum cru (ex.: "EmEstoque"). */
export const rotuloStatus: Record<StatusDispositivo, string> = {
  EmUso: 'Em uso',
  EmEstoque: 'Em estoque',
  EmManutencao: 'Em manutenção',
  Bloqueado: 'Bloqueado',
  Baixado: 'Baixado',
}

/** Cor do Chip por status. */
export const corStatus: Record<StatusDispositivo, 'success' | 'default' | 'warning' | 'error'> = {
  EmUso: 'success',
  EmEstoque: 'default',
  EmManutencao: 'warning',
  Bloqueado: 'error',
  Baixado: 'default',
}

/**
 * Status que NÃO aceitam atribuição a detentor. Espelha `DispositivosController.Atribuir`:
 * baixado/em manutenção ficariam com dono e status incoerentes; bloqueado é estado de segurança
 * que só sai pelo comando remoto "Desbloquear".
 */
const STATUS_SEM_ATRIBUICAO: readonly StatusDispositivo[] = ['Baixado', 'EmManutencao', 'Bloqueado']

/** True se o dispositivo, neste status, pode ser atribuído a um usuário. */
export function statusPermiteAtribuir(status: StatusDispositivo): boolean {
  return !STATUS_SEM_ATRIBUICAO.includes(status)
}

/** Por que a atribuição está indisponível (texto para o ADM), ou null se é permitida. */
export function motivoSemAtribuicao(status: StatusDispositivo): string | null {
  switch (status) {
    case 'Baixado':
      return 'Dispositivo baixado. Devolva-o ao estoque antes de atribuir.'
    case 'EmManutencao':
      return 'Dispositivo em manutenção. Conclua o reparo antes de atribuir.'
    case 'Bloqueado':
      return 'Dispositivo bloqueado. Desbloqueie pelo comando remoto antes de atribuir.'
    default:
      return null
  }
}
