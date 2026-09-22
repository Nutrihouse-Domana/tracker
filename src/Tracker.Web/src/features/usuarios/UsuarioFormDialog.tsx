import { useEffect, useState } from 'react'
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Button,
  Alert,
  Stack,
  FormControlLabel,
  Switch,
} from '@mui/material'
import type { Usuario } from '../../api/types'
import { useAtualizarUsuario, useCriarUsuario } from './hooks'
import { apenasDigitos, formatarCpf, formatarTelefone } from '../../utils/formato'
import { mensagemErroApi } from '../../utils/erroApi'

interface Props {
  open: boolean
  onClose: () => void
  /** Quando informado, o diálogo entra em modo de edição. */
  usuario?: Usuario | null
}

export function UsuarioFormDialog({ open, onClose, usuario }: Props) {
  const edicao = !!usuario
  const [nome, setNome] = useState('')
  const [cpf, setCpf] = useState('')
  const [email, setEmail] = useState('')
  const [telefone, setTelefone] = useState('')
  const [ativo, setAtivo] = useState(true)
  const criar = useCriarUsuario()
  const atualizar = useAtualizarUsuario()
  const mutacao = edicao ? atualizar : criar

  // Preenche (ou limpa) os campos ao abrir o diálogo.
  useEffect(() => {
    if (!open) return
    setNome(usuario?.nome ?? '')
    setCpf(formatarCpf(usuario?.cpf ?? ''))
    setEmail(usuario?.email ?? '')
    setTelefone(formatarTelefone(usuario?.telefone ?? ''))
    setAtivo(usuario?.ativo ?? true)
    criar.reset()
    atualizar.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, usuario])

  const handleClose = () => {
    criar.reset()
    atualizar.reset()
    onClose()
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const telefoneDigitos = apenasDigitos(telefone)

    if (edicao) {
      atualizar.mutate(
        { id: usuario!.id, data: { nome, email, telefone: telefoneDigitos, ativo } },
        { onSuccess: handleClose },
      )
    } else {
      criar.mutate({ nome, cpf: apenasDigitos(cpf), email, telefone: telefoneDigitos }, { onSuccess: handleClose })
    }
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>{edicao ? 'Editar usuário' : 'Novo usuário'}</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {mutacao.isError && (
              <Alert severity="error">
                {mensagemErroApi(
                  mutacao.error,
                  edicao
                    ? 'Não foi possível salvar as alterações. Verifique os dados (o email pode já estar em uso).'
                    : 'Não foi possível criar o usuário. Verifique os dados (CPF/email podem já estar cadastrados).',
                )}
              </Alert>
            )}
            <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} required fullWidth />
            <TextField
              label="CPF"
              value={cpf}
              onChange={(e) => setCpf(formatarCpf(e.target.value))}
              required
              fullWidth
              placeholder="000.000.000-00"
              // CPF identifica o usuário e fica criptografado em repouso: não é editável.
              disabled={edicao}
              helperText={edicao ? 'O CPF não pode ser alterado.' : undefined}
              slotProps={{ htmlInput: { inputMode: 'numeric' } }}
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
              onChange={(e) => setTelefone(formatarTelefone(e.target.value))}
              required
              fullWidth
              placeholder="(11) 91234-5678"
              slotProps={{ htmlInput: { inputMode: 'tel' } }}
            />
            {edicao && (
              <FormControlLabel
                control={<Switch checked={ativo} onChange={(e) => setAtivo(e.target.checked)} />}
                label={ativo ? 'Ativo' : 'Inativo'}
              />
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={mutacao.isPending}>
            {mutacao.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
