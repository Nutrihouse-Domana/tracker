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
import { useDefinirIdentificacaoInterna } from './hooks'
import { mensagemErroApi } from '../../utils/erroApi'

interface Props {
  dispositivo: Dispositivo | null
  onClose: () => void
}

/**
 * Define (ou limpa) a identificação interna do dispositivo: um rótulo próprio da TI, à parte do
 * hostname que o agente reporta. Quando preenchido, é o que aparece como identificação do aparelho.
 * Exige a senha do ADM logado.
 */
export function EditarIdentificacaoInternaDialog({ dispositivo, onClose }: Props) {
  const [identificacao, setIdentificacao] = useState('')
  const [senha, setSenha] = useState('')
  const [motivo, setMotivo] = useState('')
  const definir = useDefinirIdentificacaoInterna()

  useEffect(() => {
    if (!dispositivo) return
    setIdentificacao(dispositivo.identificacaoInterna ?? '')
    setSenha('')
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
          // Vazio limpa o campo e volta a exibir o hostname/número de série.
          identificacaoInterna: identificacao.trim() || null,
          senha,
          motivo: motivo.trim() || null,
        },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Identificação interna</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2}>
            <DialogContentText>
              Rótulo próprio da TI para este ativo. Quando preenchido, é ele que aparece como
              identificação do dispositivo — sem ser sobrescrito pelo agente. Deixe em branco para
              voltar a exibir o hostname/número de série.
            </DialogContentText>

            {definir.isError && (
              <Alert severity="error">
                {mensagemErroApi(definir.error, 'Não foi possível salvar a identificação interna.')}
              </Alert>
            )}

            <TextField
              label="Identificação interna"
              value={identificacao}
              onChange={(e) => setIdentificacao(e.target.value)}
              fullWidth
              autoFocus
              slotProps={{ htmlInput: { maxLength: 100 } }}
              placeholder={dispositivo.hostname ?? dispositivo.numeroSerie}
            />
            <TextField
              label="Motivo (opcional)"
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              fullWidth
              slotProps={{ htmlInput: { maxLength: 400 } }}
            />
            <TextField
              label="Sua senha do Tracker"
              type="password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              required
              fullWidth
              autoComplete="current-password"
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={definir.isPending || !senha}>
            {definir.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
