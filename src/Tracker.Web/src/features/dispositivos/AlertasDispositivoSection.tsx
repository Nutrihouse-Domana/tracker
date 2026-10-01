import { Box, Button, List, ListItem, ListItemText, Typography } from '@mui/material'
import type { TipoAlertaDispositivo } from '../../api/types'
import { useAlertasDispositivo, useReconhecerAlerta } from './hooks'
import { usePermissoes, PERMISSOES } from '../auth/permissoes'
import { formatarDataHora } from '../../utils/formato'

const rotuloAlerta: Record<TipoAlertaDispositivo, string> = {
  AdminDesativado: 'Permissão de administrador desativada no aparelho',
  LocalizacaoNegada: 'Permissão de localização negada ou revogada',
  BloqueioReaplicado: 'Bloqueio reaplicado pelo agente',
  ProtecaoRebaixada: 'Nível de proteção do agente caiu',
  ImpressoraOffline: 'Impressora parou de responder',
  SuprimentoBaixo: 'Suprimento baixo (toner/tinta)',
}

/** Alerta de impressora é manutenção: tratar pede `dispositivos.cadastrar`, não `dispositivos.bloquear`. */
function ehAlertaDeImpressora(tipo: TipoAlertaDispositivo) {
  return tipo === 'ImpressoraOffline' || tipo === 'SuprimentoBaixo'
}

interface Props {
  dispositivoId: string
  /** Título do bloco (ex.: "Alertas do aparelho", "Alertas da impressora"). */
  titulo: string
}

/**
 * Alertas pendentes do dispositivo, com o botão "Tratar". Espelha a regra de permissão de
 * `DispositivosAgenteController.ReconhecerAlerta`. Não renderiza nada sem pendências.
 */
export function AlertasDispositivoSection({ dispositivoId, titulo }: Props) {
  const { data: alertas } = useAlertasDispositivo(dispositivoId)
  const reconhecer = useReconhecerAlerta(dispositivoId)
  const { tem } = usePermissoes()

  const pendentes = alertas?.filter((a) => !a.reconhecido) ?? []
  if (pendentes.length === 0) return null

  const podeTratar = (tipo: TipoAlertaDispositivo) =>
    tem(ehAlertaDeImpressora(tipo) ? PERMISSOES.dispositivosCadastrar : PERMISSOES.dispositivosBloquear)

  return (
    <Box>
      <Typography variant="subtitle2" gutterBottom>
        {titulo} ({pendentes.length})
      </Typography>
      <List dense>
        {pendentes.map((a) => (
          <ListItem
            key={a.id}
            divider
            secondaryAction={
              podeTratar(a.tipo) ? (
                <Button size="small" disabled={reconhecer.isPending} onClick={() => reconhecer.mutate(a.id)}>
                  Tratar
                </Button>
              ) : null
            }
          >
            <ListItemText
              primary={rotuloAlerta[a.tipo] ?? a.tipo}
              secondary={`${formatarDataHora(a.criadoEm)}${a.detalhe ? ` — ${a.detalhe}` : ''}`}
            />
          </ListItem>
        ))}
      </List>
    </Box>
  )
}
