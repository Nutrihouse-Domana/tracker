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
import { DIAS_DE_ALERTA, paraCampoData } from './vencimento'

interface Props {
  dispositivo: Dispositivo | null
  onClose: () => void
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

  const dispositivoId = dispositivo?.id
  // Reseta só quando o diálogo ABRE ou troca de aparelho (muda o id). O objeto `dispositivo` é
  // recriado a cada refetch da query e, com ele na dependência, a senha/o texto digitado sumia.
  useEffect(() => {
    if (!dispositivo) return
    setVencimento(paraCampoData(dispositivo.vencimentoAluguel))
    setMotivo('')
    definir.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dispositivoId])

  if (!dispositivo) return null

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    definir.mutate(
      {
        id: dispositivo.id,
        data: {
          // Campo vazio limpa o vencimento (ativo sem contrato). Vai como data pura ("aaaa-mm-dd"),
          // que é o formato do DateOnly: anexar hora traria o fuso de volta para o problema.
          vencimentoAluguel: vencimento || null,
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
