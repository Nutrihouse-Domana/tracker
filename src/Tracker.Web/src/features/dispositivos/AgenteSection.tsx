import { useState } from 'react'
import {
  Stack,
  Typography,
  Button,
  Alert,
  Divider,
  List,
  ListItem,
  ListItemText,
  Chip,
  TextField,
  Box,
} from '@mui/material'
import LockIcon from '@mui/icons-material/LockOutlined'
import LockOpenIcon from '@mui/icons-material/LockOpenOutlined'
import type { Dispositivo, StatusComandoRemoto } from '../../api/types'
import { useComandos, useEmitirComando, useGerarEnrollment } from './hooks'

interface Props {
  dispositivo: Dispositivo
}

const corStatusComando: Record<StatusComandoRemoto, 'warning' | 'info' | 'success' | 'error'> = {
  Pendente: 'warning',
  Enviado: 'info',
  Confirmado: 'success',
  Falhou: 'error',
}

export function AgenteSection({ dispositivo }: Props) {
  const [token, setToken] = useState<string | null>(null)
  const { data: comandos } = useComandos(dispositivo.id)
  const emitir = useEmitirComando(dispositivo.id)
  const enrollment = useGerarEnrollment(dispositivo.id)

  const gerarToken = () => {
    enrollment.mutate(undefined, { onSuccess: (r) => setToken(r.token) })
  }

  return (
    <Stack spacing={2} sx={{ mt: 1 }}>
      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Status do agente
        </Typography>
        <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
          <Chip label={dispositivo.status} color={dispositivo.status === 'Bloqueado' ? 'error' : 'default'} />
          <Typography variant="body2" color="text.secondary">
            Último heartbeat:{' '}
            {dispositivo.ultimoHeartbeatEm
              ? new Date(dispositivo.ultimoHeartbeatEm).toLocaleString('pt-BR')
              : 'nunca'}
          </Typography>
          {dispositivo.agenteVersao && (
            <Typography variant="body2" color="text.secondary">
              Agente v{dispositivo.agenteVersao}
            </Typography>
          )}
        </Stack>
      </Box>

      <Divider />

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Bloqueio remoto
        </Typography>
        <Stack direction="row" spacing={1}>
          <Button
            variant="contained"
            color="error"
            startIcon={<LockIcon />}
            disabled={emitir.isPending}
            onClick={() => emitir.mutate('Bloquear')}
          >
            Bloquear
          </Button>
          <Button
            variant="outlined"
            startIcon={<LockOpenIcon />}
            disabled={emitir.isPending}
            onClick={() => emitir.mutate('Desbloquear')}
          >
            Desbloquear
          </Button>
        </Stack>
        <Typography variant="caption" color="text.secondary" sx={{ mt: 1, display: 'block' }}>
          O comando é entregue ao agente em tempo real (ou no próximo heartbeat, se offline).
        </Typography>
      </Box>

      <Divider />

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Instalação do agente
        </Typography>
        <Button variant="outlined" onClick={gerarToken} disabled={enrollment.isPending}>
          {enrollment.isPending ? 'Gerando...' : 'Gerar token do agente'}
        </Button>
        {token && (
          <Alert severity="success" sx={{ mt: 1 }}>
            <Typography variant="body2" sx={{ mb: 1 }}>
              Copie este token e cole no <code>appsettings.json</code> do agente na máquina. Ele aparece só uma vez:
            </Typography>
            <TextField
              value={token}
              fullWidth
              size="small"
              slotProps={{ input: { readOnly: true } }}
              onFocus={(e) => e.target.select()}
            />
          </Alert>
        )}
      </Box>

      <Divider />

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Histórico de comandos
        </Typography>
        <List dense>
          {comandos?.map((c) => (
            <ListItem
              key={c.id}
              divider
              secondaryAction={<Chip size="small" label={c.status} color={corStatusComando[c.status]} />}
            >
              <ListItemText
                primary={c.tipoComando}
                secondary={new Date(c.dataEmissao).toLocaleString('pt-BR')}
              />
            </ListItem>
          ))}
          {comandos?.length === 0 && (
            <Typography variant="body2" color="text.secondary" sx={{ p: 1 }}>
              Nenhum comando emitido ainda.
            </Typography>
          )}
        </List>
      </Box>
    </Stack>
  )
}
