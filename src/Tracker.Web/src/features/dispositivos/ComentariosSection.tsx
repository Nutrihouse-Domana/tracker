import { useState } from 'react'
import { List, ListItem, ListItemText, Typography, TextField, Button, Stack } from '@mui/material'
import { useComentarios, useCriarComentario } from './hooks'
import { formatarDataHora } from '../../utils/formato'
import { usePermissao, PERMISSOES } from '../auth/permissoes'

interface Props {
  dispositivoId: string
}

export function ComentariosSection({ dispositivoId }: Props) {
  const [texto, setTexto] = useState('')
  const { data: comentarios } = useComentarios(dispositivoId)
  const podeMovimentar = usePermissao(PERMISSOES.dispositivosMovimentar)
  const criar = useCriarComentario(dispositivoId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!texto.trim()) return
    criar.mutate({ texto }, { onSuccess: () => setTexto('') })
  }

  return (
    <Stack spacing={2} sx={{ mt: 1 }}>
      {podeMovimentar && (
        <Stack component="form" onSubmit={handleSubmit} direction="row" spacing={1}>
          <TextField
            label="Novo comentário"
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            required
            fullWidth
            size="small"
            multiline
            minRows={2}
          />
          <Button type="submit" variant="contained" disabled={criar.isPending}>
            Enviar
          </Button>
        </Stack>
      )}

      <List dense>
        {comentarios?.map((c) => (
          <ListItem key={c.id} divider alignItems="flex-start">
            <ListItemText
              primary={c.texto}
              secondary={`${c.criadoPorAdmNome} — ${formatarDataHora(c.criadoEm)}`}
            />
          </ListItem>
        ))}
        {comentarios?.length === 0 && (
          <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
            Nenhum comentário ainda.
          </Typography>
        )}
      </List>
    </Stack>
  )
}
