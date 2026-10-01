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
import BatteryIcon from '@mui/icons-material/BatteryStdOutlined'
import type {
  Dispositivo,
  NivelProtecaoAgente,
  StatusComandoRemoto,
} from '../../api/types'
import {
  useAlertasDispositivo,
  useComandos,
  useEmitirComando,
  useGerarEnrollment,
} from './hooks'
import { rotuloStatus } from './status'
import { usePermissoes, PERMISSOES } from '../auth/permissoes'
import { formatarDataHora } from '../../utils/formato'
import { AlertasDispositivoSection } from './AlertasDispositivoSection'

interface Props {
  dispositivo: Dispositivo
}

const corStatusComando: Record<StatusComandoRemoto, 'warning' | 'info' | 'success' | 'error'> = {
  Pendente: 'warning',
  Enviado: 'info',
  Confirmado: 'success',
  Falhou: 'error',
}

/**
 * Como a proteção do agente aparece para a TI. Só existe no Android: no Windows o agente roda
 * como Serviço e o campo vem nulo.
 */
const protecaoAgente: Record<
  NivelProtecaoAgente,
  { rotulo: string; cor: 'success' | 'warning' | 'error'; explicacao: string }
> = {
  DeviceOwner: {
    rotulo: 'Proteção máxima (Device Owner)',
    cor: 'success',
    explicacao: 'O agente não pode ser desinstalado e a restauração de fábrica está bloqueada.',
  },
  DeviceAdmin: {
    rotulo: 'Administrador ativo',
    cor: 'warning',
    explicacao:
      'O bloqueio remoto funciona, mas o usuário pode desativar o administrador nas Configurações e então desinstalar o agente. Você é avisado aqui se isso acontecer.',
  },
  Nenhuma: {
    rotulo: 'Sem proteção',
    cor: 'error',
    explicacao:
      'O agente está sem permissão de administrador: o bloqueio remoto NÃO vai funcionar. Reative pelo próprio app no aparelho.',
  },
}

export function AgenteSection({ dispositivo }: Props) {
  const [token, setToken] = useState<string | null>(null)
  const { data: comandos } = useComandos(dispositivo.id)
  const { data: alertas } = useAlertasDispositivo(dispositivo.id)
  const emitir = useEmitirComando(dispositivo.id)
  const enrollment = useGerarEnrollment(dispositivo.id)
  const { tem } = usePermissoes()
  const podeBloquear = tem(PERMISSOES.dispositivosBloquear)
  const podeCadastrar = tem(PERMISSOES.dispositivosCadastrar)

  const gerarToken = () => {
    enrollment.mutate(undefined, { onSuccess: (r) => setToken(r.token) })
  }

  const protecao = dispositivo.nivelProtecao ? protecaoAgente[dispositivo.nivelProtecao] : null
  const alertasPendentes = alertas?.filter((a) => !a.reconhecido) ?? []

  return (
    <Stack spacing={2} sx={{ mt: 1 }}>
      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Status do agente
        </Typography>
        <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
          <Chip label={rotuloStatus[dispositivo.status] ?? dispositivo.status} color={dispositivo.status === 'Bloqueado' ? 'error' : 'default'} />
          <Typography variant="body2" color="text.secondary">
            Último heartbeat:{' '}
            {dispositivo.ultimoHeartbeatEm ? formatarDataHora(dispositivo.ultimoHeartbeatEm) : 'nunca'}
          </Typography>
          {dispositivo.agenteVersao && (
            <Typography variant="body2" color="text.secondary">
              Agente v{dispositivo.agenteVersao}
            </Typography>
          )}
          {dispositivo.bateriaPercentual !== null && (
            <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
              <BatteryIcon fontSize="small" color="action" />
              <Typography variant="body2" color="text.secondary">
                {dispositivo.bateriaPercentual}%
              </Typography>
            </Stack>
          )}
        </Stack>

        {(dispositivo.fabricante || dispositivo.modelo) && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            {[dispositivo.fabricante, dispositivo.modelo].filter(Boolean).join(' ')}
          </Typography>
        )}
      </Box>

      {/* Proteção só é reportada pelo agente Android; no Windows a seção não aparece. */}
      {protecao && (
        <>
          <Divider />
          <Box>
            <Typography variant="subtitle2" gutterBottom>
              Proteção do agente
            </Typography>
            <Alert severity={protecao.cor}>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {protecao.rotulo}
              </Typography>
              <Typography variant="body2">{protecao.explicacao}</Typography>
            </Alert>
          </Box>
        </>
      )}

      {alertasPendentes.length > 0 && (
        <>
          <Divider />
          <AlertasDispositivoSection dispositivoId={dispositivo.id} titulo="Alertas do aparelho" />
        </>
      )}

      {podeBloquear && (
        <>
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
        </>
      )}

      {podeCadastrar && (
        <>
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
                  {dispositivo.tipo === 'Notebook'
                    ? 'Copie este token e cole no appsettings.json do agente na máquina. Ele aparece só uma vez:'
                    : 'Token individual do aparelho. No Android o agente costuma se registrar sozinho pela chave da empresa; use este token só para reconectar um aparelho manualmente. Aparece só uma vez:'}
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
        </>
      )}

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
              <ListItemText primary={c.tipoComando} secondary={formatarDataHora(c.dataEmissao)} />
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
