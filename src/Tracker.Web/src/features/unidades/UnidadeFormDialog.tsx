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
import type { Unidade } from '../../api/types'
import { useAtualizarUnidade, useCriarUnidade } from './hooks'
import { mensagemErroApi } from '../../utils/erroApi'

interface Props {
  open: boolean
  onClose: () => void
  /** Quando informada, o diálogo entra em modo de edição. */
  unidade?: Unidade | null
}

export function UnidadeFormDialog({ open, onClose, unidade }: Props) {
  const edicao = !!unidade
  const [nome, setNome] = useState('')
  const [codigo, setCodigo] = useState('')
  const [ativa, setAtiva] = useState(true)
  const criar = useCriarUnidade()
  const atualizar = useAtualizarUnidade()
  const mutacao = edicao ? atualizar : criar

  // Preenche (ou limpa) os campos ao abrir o diálogo.
  useEffect(() => {
    if (!open) return
    setNome(unidade?.nome ?? '')
    setCodigo(unidade?.codigo ?? '')
    setAtiva(unidade?.ativa ?? true)
    criar.reset()
    atualizar.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, unidade])

  const handleClose = () => {
    criar.reset()
    atualizar.reset()
    onClose()
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const dados = { nome, codigo: codigo.trim() || null }

    if (edicao) {
      atualizar.mutate({ id: unidade!.id, data: { ...dados, ativa } }, { onSuccess: handleClose })
    } else {
      criar.mutate(dados, { onSuccess: handleClose })
    }
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>{edicao ? 'Editar unidade' : 'Nova unidade'}</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {mutacao.isError && (
              <Alert severity="error">
                {mensagemErroApi(mutacao.error, 'Não foi possível salvar a unidade.')}
              </Alert>
            )}
            <TextField
              label="Nome"
              value={nome}
              onChange={(e) => setNome(e.target.value)}
              required
              fullWidth
              placeholder="Matriz, Filial Campinas..."
            />
            <TextField
              label="Código (opcional)"
              value={codigo}
              onChange={(e) => setCodigo(e.target.value)}
              fullWidth
              placeholder="MTZ, FIL01..."
              slotProps={{ htmlInput: { maxLength: 20 } }}
            />
            {edicao && (
              <FormControlLabel
                control={<Switch checked={ativa} onChange={(e) => setAtiva(e.target.checked)} />}
                label={ativa ? 'Ativa' : 'Inativa'}
              />
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={mutacao.isPending || !nome.trim()}>
            {mutacao.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
