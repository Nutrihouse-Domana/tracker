import { List, ListItem, ListItemText, Typography } from '@mui/material'
import { useHistoricoMovimentacao } from './hooks'
import { formatarDataHora } from '../../utils/formato'

interface Props {
  dispositivoId: string
}

export function MovimentacaoSection({ dispositivoId }: Props) {
  const { data: historico } = useHistoricoMovimentacao(dispositivoId)

  return (
    <List dense>
      {historico?.map((h) => (
        <ListItem key={h.id} divider>
          <ListItemText
            primary={`${h.usuarioOrigemNome ?? 'Estoque'} → ${h.usuarioDestinoNome ?? 'Estoque'}`}
            secondary={
              <>
                {formatarDataHora(h.dataMovimentacao)}
                {h.registradoPorAdmNome ? ` — por ${h.registradoPorAdmNome}` : ''}
                {h.motivo ? ` — ${h.motivo}` : ''}
              </>
            }
          />
        </ListItem>
      ))}
      {historico?.length === 0 && (
        <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
          Nenhuma movimentação registrada ainda.
        </Typography>
      )}
    </List>
  )
}
