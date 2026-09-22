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
  TableSortLabel,
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
  TablePagination,
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
import { ehAlugado, identificacaoExibida, rotuloAluguel } from '../features/dispositivos/aluguel'
import { useUnidades } from '../features/unidades/hooks'
import { usePermissoes, PERMISSOES } from '../features/auth/permissoes'
import { usePaginacao } from '../utils/paginacao'

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

type ColunaOrdenavel = 'identificacao' | 'patrimonio' | 'aluguel' | 'unidade' | 'responsavel' | 'status'
type Direcao = 'asc' | 'desc'

/**
 * Chave de comparação de cada coluna. Na aba Arquivados a última coluna é a data de
 * exclusão (ISO string, que ordena cronologicamente); nas demais é o rótulo do status.
 */
function valorOrdenacao(d: Dispositivo, coluna: ColunaOrdenavel, arquivados: boolean): string {
  switch (coluna) {
    case 'identificacao':
      return identificacaoExibida(d)
    case 'patrimonio':
      return d.assetTag ?? ''
    case 'aluguel':
      return rotuloAluguel(d)
    case 'unidade':
      return d.unidadeNome ?? ''
    case 'responsavel':
      return d.responsavelNome ?? ''
    case 'status':
      return arquivados ? (d.excluidoEm ?? '') : (rotuloStatus[d.status] ?? d.status)
  }
}

export function DispositivosPage() {
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const termoBusca = searchParams.get('q') ?? ''
  const tipoInicial = abas.findIndex((a) => a.tipo === searchParams.get('tipo'))
  const [aba, setAba] = useState(tipoInicial > 0 ? tipoInicial : 0)
  const [status, setStatus] = useState<StatusDispositivo | undefined>(
    (searchParams.get('status') as StatusDispositivo | null) ?? undefined,
  )
  const [ordenarPor, setOrdenarPor] = useState<ColunaOrdenavel>('identificacao')
  const [direcao, setDirecao] = useState<Direcao>('asc')
  const [dialogAberto, setDialogAberto] = useState(false)
  const [dispositivoParaAtribuir, setDispositivoParaAtribuir] = useState<Dispositivo | null>(null)
  const [dispositivoParaExcluir, setDispositivoParaExcluir] = useState<Dispositivo | null>(null)

  const { tem } = usePermissoes()
  const podeCadastrar = tem(PERMISSOES.dispositivosCadastrar)
  const podeMovimentar = tem(PERMISSOES.dispositivosMovimentar)
  const podeExcluir = tem(PERMISSOES.dispositivosExcluir)
  const podeExcluirDefinitivo = tem(PERMISSOES.dispositivosExcluirDefinitivo)

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
        d.identificacaoInterna,
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

  // Ordenação client-side (o filtro de busca já é client-side). Valores vazios vão sempre
  // para o fim, independente da direção, para não empurrar linhas em branco ao topo.
  const dispositivosOrdenados = useMemo(() => {
    if (!dispositivos) return dispositivos
    const fator = direcao === 'asc' ? 1 : -1
    return [...dispositivos].sort((a, b) => {
      const va = valorOrdenacao(a, ordenarPor, mostrandoArquivados)
      const vb = valorOrdenacao(b, ordenarPor, mostrandoArquivados)
      if (!va && !vb) return 0
      if (!va) return 1
      if (!vb) return -1
      return va.localeCompare(vb, 'pt-BR', { numeric: true, sensitivity: 'base' }) * fator
    })
  }, [dispositivos, ordenarPor, direcao, mostrandoArquivados])

  const { itensPagina, total, setPagina, paginacaoProps } = usePaginacao(dispositivosOrdenados)

  // Trocar de aba, status, unidade ou busca reinicia a paginação (senão ficaria numa página
  // que não existe mais na nova lista).
  useEffect(() => {
    setPagina(0)
  }, [aba, status, unidadeId, termoBusca, setPagina])

  const alternarOrdenacao = (coluna: ColunaOrdenavel) => {
    if (ordenarPor === coluna) {
      setDirecao((d) => (d === 'asc' ? 'desc' : 'asc'))
    } else {
      setOrdenarPor(coluna)
      setDirecao('asc')
    }
  }

  const cabecalho = (coluna: ColunaOrdenavel, label: string, align: 'left' | 'center' = 'left') => (
    <TableCell align={align} sortDirection={ordenarPor === coluna ? direcao : false}>
      <TableSortLabel
        active={ordenarPor === coluna}
        direction={ordenarPor === coluna ? direcao : 'asc'}
        onClick={() => alternarOrdenacao(coluna)}
      >
        {label}
      </TableSortLabel>
    </TableCell>
  )

  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h5">Dispositivos ({total})</Typography>
        {podeCadastrar && (
          <Button variant="contained" onClick={() => setDialogAberto(true)}>
            Novo dispositivo
          </Button>
        )}
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
                {cabecalho('identificacao', 'Identificação')}
                {cabecalho('patrimonio', 'Patrimônio')}
                {/* Alugado x próprio: derivado do prefixo "MAQ" na identificação. */}
                {cabecalho('aluguel', 'Aluguel', 'center')}
                {cabecalho('unidade', 'Unidade')}
                {/* Usuário nominal ou, nos ativos do local, o nome da unidade. */}
                {cabecalho('responsavel', 'Responsável')}
                {cabecalho('status', mostrandoArquivados ? 'Excluído em' : 'Status', 'center')}
                <TableCell align="right">Ações</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {itensPagina?.map((d) => (
                <TableRow key={d.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/dispositivos/${d.id}`)}>
                  <TableCell>{identificacaoExibida(d)}</TableCell>
                  <TableCell>{d.assetTag ?? '—'}</TableCell>
                  <TableCell align="center">
                    <Chip
                      size="small"
                      label={rotuloAluguel(d)}
                      color={ehAlugado(d) ? 'warning' : 'default'}
                      variant={ehAlugado(d) ? 'filled' : 'outlined'}
                    />
                  </TableCell>
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
                          {podeExcluir && (
                            <Button
                              size="small"
                              startIcon={<RestoreIcon />}
                              disabled={restaurar.isPending}
                              onClick={() => restaurar.mutate(d.id)}
                            >
                              Restaurar
                            </Button>
                          )}
                          {podeExcluirDefinitivo && (
                            <IconButton
                              size="small"
                              color="error"
                              title="Excluir definitivamente"
                              onClick={() => setDispositivoParaExcluir(d)}
                            >
                              <DeleteForeverIcon fontSize="small" />
                            </IconButton>
                          )}
                        </>
                      ) : (
                        <>
                          {/* Ativo do local não tem detentor: atribuir aqui não faz sentido. */}
                          {podeMovimentar && d.pertenceAUsuario && (
                            <Button size="small" onClick={() => setDispositivoParaAtribuir(d)}>
                              Atribuir
                            </Button>
                          )}
                          <Button size="small" onClick={() => navigate(`/dispositivos/${d.id}`)}>
                            Ver detalhes
                          </Button>
                          {podeExcluir && (
                            <IconButton
                              size="small"
                              color="error"
                              title="Excluir"
                              onClick={() => setDispositivoParaExcluir(d)}
                            >
                              <DeleteOutlineIcon fontSize="small" />
                            </IconButton>
                          )}
                        </>
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
              {dispositivosOrdenados?.length === 0 && (
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
          {total > 0 && <TablePagination {...paginacaoProps} />}
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
