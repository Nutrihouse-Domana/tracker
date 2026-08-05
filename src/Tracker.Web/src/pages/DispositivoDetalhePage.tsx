import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
  Typography,
  Paper,
  Box,
  Stack,
  Tabs,
  Tab,
  CircularProgress,
  Button,
  Grid,
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBackOutlined'
import { useDispositivo } from '../features/dispositivos/hooks'
import { StatusDispositivoAcoes } from '../features/dispositivos/StatusDispositivoAcoes'
import { MovimentacaoSection } from '../features/dispositivos/MovimentacaoSection'
import { ReparosSection } from '../features/dispositivos/ReparosSection'
import { ComentariosSection } from '../features/dispositivos/ComentariosSection'
import { AgenteSection } from '../features/dispositivos/AgenteSection'
import { LocalizacaoSection } from '../features/dispositivos/LocalizacaoSection'
import { TermoDeUsoSection } from '../features/termos/TermoDeUsoSection'

export function DispositivoDetalhePage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [aba, setAba] = useState(0)
  const { data: dispositivo, isLoading } = useDispositivo(id)

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
        <CircularProgress />
      </Box>
    )
  }

  if (!dispositivo) {
    return <Typography>Dispositivo não encontrado.</Typography>
  }

  const temAgente = dispositivo.tipo !== 'Periferico'

  const tabs: { label: string; render: () => React.ReactNode }[] = [
    {
      label: 'Visão geral',
      render: () => (
        <Typography variant="body2" color="text.secondary">
          {dispositivo.observacoes || 'Sem observações registradas.'}
        </Typography>
      ),
    },
    { label: 'Movimentação', render: () => <MovimentacaoSection dispositivoId={dispositivo.id} /> },
    { label: 'Reparos', render: () => <ReparosSection dispositivoId={dispositivo.id} /> },
    { label: 'Comentários', render: () => <ComentariosSection dispositivoId={dispositivo.id} /> },
    ...(temAgente
      ? [
          { label: 'Agente & Bloqueio', render: () => <AgenteSection dispositivo={dispositivo} /> },
          { label: 'Localização', render: () => <LocalizacaoSection dispositivoId={dispositivo.id} /> },
          { label: 'Termo de uso', render: () => <TermoDeUsoSection dispositivo={dispositivo} /> },
        ]
      : []),
  ]

  return (
    <>
      <Button startIcon={<ArrowBackIcon />} onClick={() => navigate('/dispositivos')} sx={{ mb: 2 }}>
        Voltar
      </Button>

      <Paper sx={{ p: 3, mb: 2 }}>
        <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'flex-start', mb: 2 }}>
          <Box>
            <Typography variant="h5">{dispositivo.hostname ?? dispositivo.numeroSerie}</Typography>
            <Typography variant="body2" color="text.secondary">
              {dispositivo.tipo} — {dispositivo.assetTag ?? dispositivo.numeroSerie}
            </Typography>
          </Box>
          <StatusDispositivoAcoes dispositivoId={dispositivo.id} status={dispositivo.status} />
        </Stack>

        <Grid container spacing={2}>
          <Grid size={{ xs: 6, sm: 3 }}>
            <Typography variant="caption" color="text.secondary">Número de série</Typography>
            <Typography variant="body2">{dispositivo.numeroSerie}</Typography>
          </Grid>
          <Grid size={{ xs: 6, sm: 3 }}>
            <Typography variant="caption" color="text.secondary">Usuário atual</Typography>
            <Typography variant="body2">{dispositivo.usuarioAtualNome ?? '—'}</Typography>
          </Grid>
          {dispositivo.sistemaOperacional && (
            <Grid size={{ xs: 6, sm: 3 }}>
              <Typography variant="caption" color="text.secondary">Sistema operacional</Typography>
              <Typography variant="body2">
                {dispositivo.sistemaOperacional} {dispositivo.versaoSO}
              </Typography>
            </Grid>
          )}
          {dispositivo.imei && (
            <Grid size={{ xs: 6, sm: 3 }}>
              <Typography variant="caption" color="text.secondary">IMEI</Typography>
              <Typography variant="body2">{dispositivo.imei}</Typography>
            </Grid>
          )}
          {dispositivo.tipoPeriferico && (
            <Grid size={{ xs: 6, sm: 3 }}>
              <Typography variant="caption" color="text.secondary">Tipo de periférico</Typography>
              <Typography variant="body2">{dispositivo.tipoPeriferico}</Typography>
            </Grid>
          )}
          <Grid size={{ xs: 6, sm: 3 }}>
            <Typography variant="caption" color="text.secondary">Último heartbeat</Typography>
            <Typography variant="body2">
              {dispositivo.ultimoHeartbeatEm ? new Date(dispositivo.ultimoHeartbeatEm).toLocaleString('pt-BR') : '—'}
            </Typography>
          </Grid>
        </Grid>
      </Paper>

      <Paper>
        <Tabs
          value={aba}
          onChange={(_, v) => setAba(v)}
          variant="scrollable"
          scrollButtons="auto"
          sx={{ borderBottom: 1, borderColor: 'divider' }}
        >
          {tabs.map((t) => (
            <Tab key={t.label} label={t.label} />
          ))}
        </Tabs>
        <Box sx={{ p: 3 }}>{tabs[aba]?.render()}</Box>
      </Paper>
    </>
  )
}
