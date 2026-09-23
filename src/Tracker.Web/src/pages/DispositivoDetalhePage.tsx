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
  IconButton,
  Chip,
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBackOutlined'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined'
import EditOutlinedIcon from '@mui/icons-material/EditOutlined'
import { useDispositivo } from '../features/dispositivos/hooks'
import { PosseSection } from '../features/dispositivos/PosseSection'
import { EditarIdentificacaoInternaDialog } from '../features/dispositivos/EditarIdentificacaoInternaDialog'
import { VencimentoAluguelDialog } from '../features/dispositivos/VencimentoAluguelDialog'
import { ExcluirDispositivoDialog } from '../features/dispositivos/ExcluirDispositivoDialog'
import { StatusDispositivoAcoes } from '../features/dispositivos/StatusDispositivoAcoes'
import { MovimentacaoSection } from '../features/dispositivos/MovimentacaoSection'
import { ReparosSection } from '../features/dispositivos/ReparosSection'
import { ComentariosSection } from '../features/dispositivos/ComentariosSection'
import { AgenteSection } from '../features/dispositivos/AgenteSection'
import { LocalizacaoSection } from '../features/dispositivos/LocalizacaoSection'
import { TermoDeUsoSection } from '../features/termos/TermoDeUsoSection'
import { usePermissoes, PERMISSOES } from '../features/auth/permissoes'
import { formatarDataHora } from '../utils/formato'
import {
  corVencimento,
  formatarVencimento,
  rotuloVencimento,
  situacaoVencimento,
} from '../features/dispositivos/vencimento'

export function DispositivoDetalhePage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [aba, setAba] = useState(0)
  const [excluirAberto, setExcluirAberto] = useState(false)
  const [identificacaoAberta, setIdentificacaoAberta] = useState(false)
  const [vencimentoAberto, setVencimentoAberto] = useState(false)
  const { data: dispositivo, isLoading } = useDispositivo(id)
  const { tem } = usePermissoes()

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
    { label: 'Unidade & Posse', render: () => <PosseSection dispositivo={dispositivo} /> },
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
            <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
              {/* Identificação interna da TI tem prioridade sobre o hostname reportado pelo agente. */}
              <Typography variant="h5">
                {dispositivo.identificacaoInterna ?? dispositivo.hostname ?? dispositivo.numeroSerie}
              </Typography>
              {tem(PERMISSOES.dispositivosCadastrar) && (
                <IconButton
                  size="small"
                  aria-label="Editar identificação interna"
                  onClick={() => setIdentificacaoAberta(true)}
                >
                  <EditOutlinedIcon fontSize="small" />
                </IconButton>
              )}
            </Stack>
            <Typography variant="body2" color="text.secondary">
              {dispositivo.tipo} — {dispositivo.assetTag ?? dispositivo.numeroSerie}
            </Typography>
          </Box>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            {tem(PERMISSOES.dispositivosMovimentar) && (
              <StatusDispositivoAcoes
                dispositivoId={dispositivo.id}
                status={dispositivo.status}
                pertenceAUsuario={dispositivo.pertenceAUsuario}
                unidadeId={dispositivo.unidadeId}
              />
            )}
            {tem(PERMISSOES.dispositivosExcluir) && (
              <Button
                size="small"
                color="error"
                startIcon={<DeleteOutlineIcon />}
                onClick={() => setExcluirAberto(true)}
              >
                Excluir
              </Button>
            )}
          </Stack>
        </Stack>

        <Grid container spacing={2}>
          <Grid size={{ xs: 6, sm: 3 }}>
            <Typography variant="caption" color="text.secondary">Número de série</Typography>
            <Typography variant="body2">{dispositivo.numeroSerie}</Typography>
          </Grid>
          {/* Com identificação interna definida, o título deixa de mostrar o hostname técnico —
              então ele aparece aqui para não se perder. */}
          {dispositivo.identificacaoInterna && dispositivo.hostname && (
            <Grid size={{ xs: 6, sm: 3 }}>
              <Typography variant="caption" color="text.secondary">Hostname</Typography>
              <Typography variant="body2">{dispositivo.hostname}</Typography>
            </Grid>
          )}
          <Grid size={{ xs: 6, sm: 3 }}>
            {/* Ativo do local não tem detentor: o rótulo e o valor passam a ser da unidade. */}
            <Typography variant="caption" color="text.secondary">
              {dispositivo.pertenceAUsuario ? 'Usuário atual' : 'Unidade (ativo do local)'}
            </Typography>
            <Typography variant="body2">{dispositivo.responsavelNome ?? '—'}</Typography>
          </Grid>
          <Grid size={{ xs: 6, sm: 3 }}>
            <Typography variant="caption" color="text.secondary">Unidade atendida</Typography>
            <Typography variant="body2">{dispositivo.unidadeNome ?? '—'}</Typography>
          </Grid>
          <Grid size={{ xs: 6, sm: 3 }}>
            <Typography variant="caption" color="text.secondary">Vencimento do aluguel</Typography>
            <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
              <Typography variant="body2">{formatarVencimento(dispositivo.vencimentoAluguel)}</Typography>
              {tem(PERMISSOES.dispositivosCadastrar) && (
                <IconButton
                  size="small"
                  aria-label="Editar vencimento do aluguel"
                  onClick={() => setVencimentoAberto(true)}
                >
                  <EditOutlinedIcon fontSize="small" />
                </IconButton>
              )}
            </Stack>
            {dispositivo.vencimentoAluguel && situacaoVencimento(dispositivo.vencimentoAluguel) !== 'emDia' && (
              <Chip
                size="small"
                label={rotuloVencimento(dispositivo.vencimentoAluguel)}
                color={corVencimento(situacaoVencimento(dispositivo.vencimentoAluguel))}
              />
            )}
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
              {dispositivo.ultimoHeartbeatEm ? formatarDataHora(dispositivo.ultimoHeartbeatEm) : '—'}
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

      <VencimentoAluguelDialog
        dispositivo={vencimentoAberto ? dispositivo : null}
        onClose={() => setVencimentoAberto(false)}
      />
      <EditarIdentificacaoInternaDialog
        dispositivo={identificacaoAberta ? dispositivo : null}
        onClose={() => setIdentificacaoAberta(false)}
      />

      <ExcluirDispositivoDialog
        dispositivo={excluirAberto ? dispositivo : null}
        onClose={() => setExcluirAberto(false)}
        // O ativo sai das listas: ficar na tela dele seria um beco sem saída.
        onExcluido={() => navigate('/dispositivos')}
      />
    </>
  )
}
