import { useState } from 'react'
import {
  List,
  ListItem,
  ListItemText,
  Typography,
  TextField,
  Button,
  Stack,
  Chip,
  MenuItem,
  Select,
} from '@mui/material'
import type { StatusReparo } from '../../api/types'
import { useCriarReparo, useAtualizarStatusReparo, useReparos } from './hooks'
import { useMe } from '../auth/useAuth'

interface Props {
  dispositivoId: string
}

const corStatus: Record<StatusReparo, 'warning' | 'info' | 'success' | 'default'> = {
  Aberto: 'warning',
  EmAndamento: 'info',
  Concluido: 'success',
  Cancelado: 'default',
}

export function ReparosSection({ dispositivoId }: Props) {
  const [descricao, setDescricao] = useState('')
  const [custoEstimado, setCustoEstimado] = useState('')

  const { data: reparos } = useReparos(dispositivoId)
  const { data: adm } = useMe()
  const criar = useCriarReparo(dispositivoId)
  const atualizarStatus = useAtualizarStatusReparo(dispositivoId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!adm) return
    criar.mutate(
      {
        descricao,
        custoEstimado: custoEstimado ? Number(custoEstimado) : null,
        registradoPorAdmId: adm.id,
      },
      { onSuccess: () => { setDescricao(''); setCustoEstimado('') } },
    )
  }

  return (
    <Stack spacing={2} sx={{ mt: 1 }}>
      <Stack component="form" onSubmit={handleSubmit} direction={{ xs: 'column', sm: 'row' }} spacing={1}>
        <TextField
          label="Descrição do reparo"
          value={descricao}
          onChange={(e) => setDescricao(e.target.value)}
          required
          fullWidth
          size="small"
        />
        <TextField
          label="Custo estimado (R$)"
          type="number"
          value={custoEstimado}
          onChange={(e) => setCustoEstimado(e.target.value)}
          size="small"
          sx={{ width: { xs: '100%', sm: 200 } }}
        />
        <Button type="submit" variant="contained" disabled={criar.isPending}>
          Abrir reparo
        </Button>
      </Stack>

      <List dense>
        {reparos?.map((r) => (
          <ListItem
            key={r.id}
            divider
            secondaryAction={
              <Select
                size="small"
                value={r.status}
                onChange={(e) =>
                  atualizarStatus.mutate({ reparoId: r.id, status: e.target.value as StatusReparo })
                }
              >
                <MenuItem value="Aberto">Aberto</MenuItem>
                <MenuItem value="EmAndamento">Em andamento</MenuItem>
                <MenuItem value="Concluido">Concluído</MenuItem>
                <MenuItem value="Cancelado">Cancelado</MenuItem>
              </Select>
            }
          >
            <ListItemText
              primary={
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                  <span>{r.descricao}</span>
                  <Chip size="small" label={r.status} color={corStatus[r.status]} />
                </Stack>
              }
              secondary={
                <>
                  Aberto em {new Date(r.dataAbertura).toLocaleString('pt-BR')}
                  {r.custoEstimado != null ? ` — R$ ${r.custoEstimado.toFixed(2)}` : ''}
                  {r.dataConclusao ? ` — concluído em ${new Date(r.dataConclusao).toLocaleString('pt-BR')}` : ''}
                </>
              }
            />
          </ListItem>
        ))}
        {reparos?.length === 0 && (
          <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
            Nenhum reparo registrado ainda.
          </Typography>
        )}
      </List>
    </Stack>
  )
}
