import { useState } from 'react'
import { Dialog, DialogTitle, DialogContent, DialogActions, TextField, Button, Alert, Stack } from '@mui/material'
import { useCriarUsuario } from './hooks'

interface Props {
  open: boolean
  onClose: () => void
}

export function UsuarioFormDialog({ open, onClose }: Props) {
  const [nome, setNome] = useState('')
  const [cpf, setCpf] = useState('')
  const [email, setEmail] = useState('')
  const [telefone, setTelefone] = useState('')
  const criar = useCriarUsuario()

  const handleClose = () => {
    setNome('')
    setCpf('')
    setEmail('')
    setTelefone('')
    criar.reset()
    onClose()
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    criar.mutate(
      { nome, cpf: cpf.replace(/\D/g, ''), email, telefone },
      { onSuccess: handleClose },
    )
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>Novo usuário</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {criar.isError && (
              <Alert severity="error">
                Não foi possível criar o usuário. Verifique os dados (CPF/email podem já estar cadastrados).
              </Alert>
            )}
            <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} required fullWidth />
            <TextField
              label="CPF"
              value={cpf}
              onChange={(e) => setCpf(e.target.value)}
              required
              fullWidth
              placeholder="000.000.000-00"
            />
            <TextField
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              fullWidth
            />
            <TextField
              label="Telefone"
              value={telefone}
              onChange={(e) => setTelefone(e.target.value)}
              required
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={criar.isPending}>
            {criar.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
