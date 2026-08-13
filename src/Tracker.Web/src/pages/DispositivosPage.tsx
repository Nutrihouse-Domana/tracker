import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  Typography,
  Button,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Paper,
  TableContainer,
  Chip,
  Stack,
  CircularProgress,
  Box,
  Tabs,
  Tab,
  Alert,
  ToggleButton,
  ToggleButtonGroup,
} from '@mui/material'
import type { Dispositivo, StatusDispositivo, TipoDispositivo } from '../api/types'
import { useDispositivos } from '../features/dispositivos/hooks'
import { DispositivoFormDialog } from '../features/dispositivos/DispositivoFormDialog'
import { AtribuirDialog } from '../features/dispositivos/AtribuirDialog'
import { corStatus, rotuloStatus } from '../features/dispositivos/status'

const abas: { label: string; tipo: TipoDispositivo | undefined }[] = [
  { label: 'Todos', tipo: undefined },
  { label: 'Notebooks', tipo: 'Notebook' },
  { label: 'Mobiles', tipo: 'Mobile' },
  { label: 'Periféricos', tipo: 'Periferico' },
]

const filtrosStatus: { label: string; status: StatusDispositivo | undefined }[] = [
  { label: 'Todos', status: undefined },
  { label: 'Em uso', status: 'EmUso' },
  { label: 'Em estoque', status: 'EmEstoque' },
  { label: 'Em manutenção', status: 'EmManutencao' },
  { label: 'Bloqueado', status: 'Bloqueado' },
  { label: 'Baixado', status: 'Baixado' },
]

export function DispositivosPage() {
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const termoBusca = searchParams.get('q') ?? ''
  const tipoInicial = abas.findIndex((a) => a.tipo === searchParams.get('tipo'))
  const [aba, setAba] = useState(tipoInicial > 0 ? tipoInicial : 0)
  const [status, setStatus] = useState<StatusDispositivo | undefined>(
    (searchParams.get('status') as StatusDispositivo | null) ?? undefined,
  )
  const [dialogAberto, setDialogAberto] = useState(false)
  const [dispositivoParaAtribuir, setDispositivoParaAtribuir] = useState<Dispositivo | null>(null)

  const { data: dispositivosCarregados, isLoading } = useDispositivos({ tipo: abas[aba].tipo, status })

  // Uma nova busca zera aba e filtro de status: senão o termo "não acha nada" só porque
  // havia um filtro ativo que exclui o dispositivo procurado.
  useEffect(() => {
    if (!termoBusca.trim()) return
    setAba(0)
    setStatus(undefined)
  }, [termoBusca])

  const dispositivos = useMemo(() => {
    if (!termoBusca.trim()) return dispositivosCarregados
    const termo = termoBusca.trim().toLowerCase()
    return dispositivosCarregados?.filter((d) =>
      [d.numeroSerie, d.assetTag, d.hostname, d.imei, d.usuarioAtualNome, d.sistemaOperacional, d.observacoes]
        .filter(Boolean)
        .some((campo) => campo!.toLowerCase().includes(termo)),
    )
  }, [dispositivosCarregados, termoBusca])

  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h5">Dispositivos</Typography>
        <Button variant="contained" onClick={() => setDialogAberto(true)}>
          Novo dispositivo
        </Button>
      </Stack>

      {termoBusca && (
        <Alert severity="info" sx={{ mb: 2 }} onClose={() => setSearchParams({})}>
          Mostrando resultados para "{termoBusca}" ({dispositivos?.length ?? 0})
        </Alert>
      )}

      <Tabs value={aba} onChange={(_, v) => setAba(v)} sx={{ mb: 2 }}>
        {abas.map((a) => (
          <Tab key={a.label} label={a.label} />
        ))}
      </Tabs>

      <ToggleButtonGroup
        value={status ?? 'Todos'}
        exclusive
        size="small"
        onChange={(_, v) => setStatus(v && v !== 'Todos' ? (v as StatusDispositivo) : undefined)}
        sx={{ mb: 2, flexWrap: 'wrap' }}
      >
        {filtrosStatus.map((f) => (
          <ToggleButton key={f.label} value={f.status ?? 'Todos'}>
            {f.label}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Tipo</TableCell>
                <TableCell>Identificação</TableCell>
                <TableCell>Patrimônio</TableCell>
                <TableCell>Usuário atual</TableCell>
                <TableCell align="center">Status</TableCell>
                <TableCell align="right">Ações</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {dispositivos?.map((d) => (
                <TableRow key={d.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/dispositivos/${d.id}`)}>
                  <TableCell>{d.tipo}</TableCell>
                  <TableCell>{d.hostname ?? d.numeroSerie}</TableCell>
                  <TableCell>{d.assetTag ?? '—'}</TableCell>
                  <TableCell>{d.usuarioAtualNome ?? '—'}</TableCell>
                  <TableCell align="center">
                    <Chip size="small" label={rotuloStatus[d.status] ?? d.status} color={corStatus[d.status] ?? 'default'} />
                  </TableCell>
                  <TableCell align="right" onClick={(e) => e.stopPropagation()}>
                    <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
                      <Button size="small" onClick={() => setDispositivoParaAtribuir(d)}>
                        Atribuir
                      </Button>
                      <Button size="small" onClick={() => navigate(`/dispositivos/${d.id}`)}>
                        Ver detalhes
                      </Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
              {dispositivos?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} align="center">
                    {termoBusca
                      ? `Nenhum dispositivo encontrado para "${termoBusca}".`
                      : 'Nenhum dispositivo cadastrado.'}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <DispositivoFormDialog open={dialogAberto} onClose={() => setDialogAberto(false)} />
      <AtribuirDialog dispositivo={dispositivoParaAtribuir} onClose={() => setDispositivoParaAtribuir(null)} />
    </>
  )
}
