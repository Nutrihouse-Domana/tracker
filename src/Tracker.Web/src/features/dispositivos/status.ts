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
