import { useEffect, useState } from 'react'
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Stack,
  TextField,
} from '@mui/material'
import type { Dispositivo } from '../../api/types'
import { useDefinirVencimentoAluguel } from './hooks'
import { mensagemErroApi } from '../../utils/erroApi'
import { DIAS_DE_ALERTA } from './vencimento'

interface Props {
  dispositivo: Dispositivo | null
  onClose: () => void
}

/** ISO do backend ("2026-10-01T00:00:00") para o formato do input date ("2026-10-01"). */
function paraCampoData(iso: string | null): string {
  if (!iso) return ''
  const data = new Date(iso)
  if (Number.isNaN(data.getTime())) return ''
  const mes = `${data.getMonth() + 1}`.padStart(2, '0')
  const dia = `${data.getDate()}`.padStart(2, '0')
  return `${data.getFullYear()}-${mes}-${dia}`
}

/**
 * Define (ou limpa) o vencimento do contrato de aluguel do equipamento. Não pede a senha do ADM:
 * é dado operacional do contrato, não mudança de responsabilidade sobre o ativo — mas a alteração
 * fica na auditoria como qualquer escrita.
 */
export function VencimentoAluguelDialog({ dispositivo, onClose }: Props) {
  const [vencimento, setVencimento] = useState('')
  const [motivo, setMotivo] = useState('')
  const definir = useDefinirVencimentoAluguel()

  useEffect(() => {
    if (!dispositivo) return
    setVencimento(paraCampoData(dispositivo.vencimentoAluguel))
    setMotivo('')
    definir.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dispositivo])

  if (!dispositivo) return null

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    definir.mutate(
      {
        id: dispositivo.id,
        data: {
          // Campo vazio limpa o vencimento (ativo sem contrato). A hora vai zerada: só a data conta.
          vencimentoAluguel: vencimento ? `${vencimento}T00:00:00` : null,
          motivo: motivo.trim() || null,
        },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Vencimento do aluguel</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2}>
            <DialogContentText>
              Data em que o contrato de aluguel deste equipamento termina. A partir de{' '}
              {DIAS_DE_ALERTA} dias do vencimento o ativo passa a ser destacado na lista. Deixe em
              branco se o equipamento não for alugado.
            </DialogContentText>

            {definir.isError && (
              <Alert severity="error">
                {mensagemErroApi(definir.error, 'Não foi possível salvar o vencimento.')}
              </Alert>
            )}

            <TextField
              label="Vence em"
              type="date"
              value={vencimento}
              onChange={(e) => setVencimento(e.target.value)}
              fullWidth
              autoFocus
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="Motivo (opcional)"
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              fullWidth
              slotProps={{ htmlInput: { maxLength: 400 } }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={definir.isPending}>
            {definir.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
