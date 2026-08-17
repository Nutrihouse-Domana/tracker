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
  IconButton,
  Tooltip,
} from '@mui/material'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined'
import RestoreIcon from '@mui/icons-material/RestoreOutlined'
import DeleteForeverIcon from '@mui/icons-material/DeleteForeverOutlined'
import type { Dispositivo, StatusDispositivo, TipoDispositivo } from '../api/types'
import {
  useDispositivos,
  useDispositivosArquivados,
  useRestaurarDispositivo,
} from '../features/dispositivos/hooks'
import { DispositivoFormDialog } from '../features/dispositivos/DispositivoFormDialog'
import { AtribuirDialog } from '../features/dispositivos/AtribuirDialog'
import { ExcluirDispositivoDialog } from '../features/dispositivos/ExcluirDispositivoDialog'
import { corStatus, rotuloStatus } from '../features/dispositivos/status'
import { useUnidades } from '../features/unidades/hooks'

/** A última aba lista os arquivados (tipo indiferente), por isso `arquivados`. */
const abas: { label: string; tipo?: TipoDispositivo; arquivados?: boolean }[] = [
  { label: 'Todos', tipo: undefined },
  { label: 'Notebooks', tipo: 'Notebook' },
  { label: 'Mobiles', tipo: 'Mobile' },
  { label: 'Periféricos', tipo: 'Periferico' },
  { label: 'Arquivados', arquivados: true },
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
  const [dispositivoParaExcluir, setDispositivoParaExcluir] = useState<Dispositivo | null>(null)

  const unidadeId = searchParams.get('unidadeId') ?? undefined
  const { data: unidades } = useUnidades()
  const unidadeFiltrada = unidades?.find((u) => u.id === unidadeId)
  const mostrandoArquivados = !!abas[aba].arquivados
  const restaurar = useRestaurarDispositivo()

  const listaAtiva = useDispositivos({ tipo: abas[aba].tipo, status, unidadeId })
  const listaArquivada = useDispositivosArquivados(mostrandoArquivados)
  const { data: dispositivosCarregados, isLoading } = mostrandoArquivados ? listaArquivada : listaAtiva

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
      [
        d.numeroSerie,
        d.assetTag,
        d.hostname,
        d.imei,
        d.usuarioAtualNome,
        d.unidadeNome,
        d.sistemaOperacional,
        d.observacoes,
      ]
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

      {unidadeFiltrada && (
        <Alert
          severity="info"
          sx={{ mb: 2 }}
          onClose={() => {
            searchParams.delete('unidadeId')
            setSearchParams(searchParams)
          }}
        >
          Mostrando apenas dispositivos da unidade <strong>{unidadeFiltrada.nome}</strong>
        </Alert>
      )}

      <Tabs value={aba} onChange={(_, v) => setAba(v)} variant="scrollable" scrollButtons="auto" sx={{ mb: 2 }}>
        {abas.map((a) => (
          <Tab key={a.label} label={a.label} />
        ))}
      </Tabs>

      {/* Arquivados não têm filtro de status: o que interessa neles é quando e por que saíram. */}
      {!mostrandoArquivados && (
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
      )}

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
                <TableCell>Unidade</TableCell>
                {/* Usuário nominal ou, nos ativos do local, o nome da unidade. */}
                <TableCell>Responsável</TableCell>
                <TableCell align="center">{mostrandoArquivados ? 'Excluído em' : 'Status'}</TableCell>
                <TableCell align="right">Ações</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {dispositivos?.map((d) => (
                <TableRow key={d.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/dispositivos/${d.id}`)}>
                  <TableCell>{d.tipo}</TableCell>
                  <TableCell>{d.hostname ?? d.numeroSerie}</TableCell>
                  <TableCell>{d.assetTag ?? '—'}</TableCell>
                  <TableCell>{d.unidadeNome ?? '—'}</TableCell>
                  <TableCell>
                    {d.responsavelNome ?? '—'}
                    {!d.pertenceAUsuario && d.responsavelNome && (
                      <Chip size="small" label="Local" variant="outlined" sx={{ ml: 1 }} />
                    )}
                  </TableCell>
                  <TableCell align="center">
                    {mostrandoArquivados ? (
                      <Tooltip title={d.motivoExclusao ?? 'Sem motivo registrado'}>
                        <span>{d.excluidoEm ? new Date(d.excluidoEm).toLocaleDateString('pt-BR') : '—'}</span>
                      </Tooltip>
                    ) : (
                      <Chip
                        size="small"
                        label={rotuloStatus[d.status] ?? d.status}
                        color={corStatus[d.status] ?? 'default'}
                      />
                    )}
                  </TableCell>
                  <TableCell align="right" onClick={(e) => e.stopPropagation()}>
                    <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end', alignItems: 'center' }}>
                      {mostrandoArquivados ? (
                        <>
                          <Button
                            size="small"
                            startIcon={<RestoreIcon />}
                            disabled={restaurar.isPending}
                            onClick={() => restaurar.mutate(d.id)}
                          >
                            Restaurar
                          </Button>
                          <IconButton
                            size="small"
                            color="error"
                            title="Excluir definitivamente"
                            onClick={() => setDispositivoParaExcluir(d)}
                          >
                            <DeleteForeverIcon fontSize="small" />
                          </IconButton>
                        </>
                      ) : (
                        <>
                          {/* Ativo do local não tem detentor: atribuir aqui não faz sentido. */}
                          {d.pertenceAUsuario && (
                            <Button size="small" onClick={() => setDispositivoParaAtribuir(d)}>
                              Atribuir
                            </Button>
                          )}
                          <Button size="small" onClick={() => navigate(`/dispositivos/${d.id}`)}>
                            Ver detalhes
                          </Button>
                          <IconButton
                            size="small"
                            color="error"
                            title="Excluir"
                            onClick={() => setDispositivoParaExcluir(d)}
                          >
                            <DeleteOutlineIcon fontSize="small" />
                          </IconButton>
                        </>
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
              {dispositivos?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} align="center">
                    {termoBusca
                      ? `Nenhum dispositivo encontrado para "${termoBusca}".`
                      : mostrandoArquivados
                        ? 'Nenhum dispositivo arquivado.'
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
      <ExcluirDispositivoDialog
        dispositivo={dispositivoParaExcluir}
        onClose={() => setDispositivoParaExcluir(null)}
      />
    </>
  )
}
