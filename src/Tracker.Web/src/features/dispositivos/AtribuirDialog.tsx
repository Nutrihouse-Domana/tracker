import { useState } from 'react'
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Alert,
  Stack,
  TextField,
  MenuItem,
} from '@mui/material'
import type { Dispositivo } from '../../api/types'
import { useAtribuirDispositivo } from './hooks'
import { useUsuarios } from '../usuarios/hooks'

interface Props {
  dispositivo: Dispositivo | null
  onClose: () => void
}

export function AtribuirDialog({ dispositivo, onClose }: Props) {
  const [usuarioId, setUsuarioId] = useState('')
  const [motivo, setMotivo] = useState('')
  const { data: usuarios } = useUsuarios()
  const atribuir = useAtribuirDispositivo()

  const handleClose = () => {
    setUsuarioId('')
    setMotivo('')
    atribuir.reset()
    onClose()
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!dispositivo) return
    atribuir.mutate(
      {
        id: dispositivo.id,
        data: { usuarioId, motivo },
      },
      { onSuccess: handleClose },
    )
  }

  return (
    <Dialog open={!!dispositivo} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>Atribuir dispositivo</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {atribuir.isError && <Alert severity="error">Não foi possível atribuir o dispositivo.</Alert>}
            <TextField
              select
              label="Usuário"
              value={usuarioId}
              onChange={(e) => setUsuarioId(e.target.value)}
              required
              fullWidth
            >
              {usuarios
                ?.filter((u) => u.ativo)
                .map((u) => (
                  <MenuItem key={u.id} value={u.id}>
                    {u.nome}
                  </MenuItem>
                ))}
            </TextField>
            <TextField
              label="Motivo"
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              fullWidth
              placeholder="Entrega inicial, troca de equipamento, etc."
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={atribuir.isPending || !usuarioId}>
            {atribuir.isPending ? 'Atribuindo...' : 'Atribuir'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
