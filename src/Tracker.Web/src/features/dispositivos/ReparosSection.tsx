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
import { formatarDataHora } from '../../utils/formato'
import { usePermissao, PERMISSOES } from '../auth/permissoes'

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
  const podeMovimentar = usePermissao(PERMISSOES.dispositivosMovimentar)
  const criar = useCriarReparo(dispositivoId)
  const atualizarStatus = useAtualizarStatusReparo(dispositivoId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    criar.mutate(
      {
        descricao,
        custoEstimado: custoEstimado ? Number(custoEstimado) : null,
      },
      { onSuccess: () => { setDescricao(''); setCustoEstimado('') } },
    )
  }

  return (
    <Stack spacing={2} sx={{ mt: 1 }}>
      {podeMovimentar && (
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
      )}

      <List dense>
        {reparos?.map((r) => (
          <ListItem
            key={r.id}
            divider
            secondaryAction={
              podeMovimentar ? (
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
              ) : null
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
                  Aberto em {formatarDataHora(r.dataAbertura)}
                  {r.registradoPorAdmNome ? ` por ${r.registradoPorAdmNome}` : ''}
                  {r.custoEstimado != null ? ` — R$ ${r.custoEstimado.toFixed(2)}` : ''}
                  {r.dataConclusao ? ` — concluído em ${formatarDataHora(r.dataConclusao)}` : ''}
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
