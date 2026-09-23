import { useMemo, useState } from 'react'
import {
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Paper,
  TableContainer,
  Stack,
  TextField,
  MenuItem,
  Box,
  CircularProgress,
  TablePagination,
  Chip,
} from '@mui/material'
import { useAcoesAuditoria, useAuditoria } from '../features/auditoria/hooks'
import { formatarDataHora } from '../utils/formato'

export function AuditoriaPage() {
  const [q, setQ] = useState('')
  const [acao, setAcao] = useState('')
  const [de, setDe] = useState('')
  const [ate, setAte] = useState('')
  const [pagina, setPagina] = useState(0) // 0-based (TablePagination)
  const [tamanho, setTamanho] = useState(10)

  const { data: acoes } = useAcoesAuditoria()
  const { data, isLoading, isFetching } = useAuditoria({
    q: q || undefined,
    acao: acao || undefined,
    de: de || undefined,
    ate: ate || undefined,
    pagina: pagina + 1,
    tamanho,
  })

  const rotuloPorAcao = useMemo(() => {
    const mapa = new Map<string, string>()
    for (const a of acoes ?? []) mapa.set(a.valor, a.rotulo)
    return mapa
  }, [acoes])

  // Qualquer mudança de filtro volta para a primeira página.
  const aoFiltrar = <T,>(setter: (v: T) => void) => (v: T) => {
    setter(v)
    setPagina(0)
  }

  return (
    <>
      <Typography variant="h5" sx={{ mb: 2 }}>
        Auditoria ({data?.total ?? 0})
      </Typography>

      <Paper sx={{ p: 2, mb: 2 }}>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
          <TextField
            label="Buscar (usuário ou descrição)"
            value={q}
            onChange={(e) => aoFiltrar(setQ)(e.target.value)}
            size="small"
            fullWidth
          />
          <TextField
            select
            label="Ação"
            value={acao}
            onChange={(e) => aoFiltrar(setAcao)(e.target.value)}
            size="small"
            sx={{ minWidth: { xs: '100%', md: 220 } }}
          >
            <MenuItem value="">Todas</MenuItem>
            {acoes?.map((a) => (
              <MenuItem key={a.valor} value={a.valor}>
                {a.rotulo}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label="De"
            type="date"
            value={de}
            onChange={(e) => aoFiltrar(setDe)(e.target.value)}
            size="small"
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            label="Até"
            type="date"
            value={ate}
            onChange={(e) => aoFiltrar(setAte)(e.target.value)}
            size="small"
            slotProps={{ inputLabel: { shrink: true } }}
          />
        </Stack>
      </Paper>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Data/hora</TableCell>
                <TableCell>Usuário</TableCell>
                <TableCell>Ação</TableCell>
                <TableCell>Descrição</TableCell>
                <TableCell>IP</TableCell>
              </TableRow>
            </TableHead>
            <TableBody sx={{ opacity: isFetching ? 0.6 : 1 }}>
              {data?.itens.map((log) => (
                <TableRow key={log.id} hover>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatarDataHora(log.dataHora)}</TableCell>
                  <TableCell>{log.admNome}</TableCell>
                  <TableCell>
                    <Chip size="small" variant="outlined" label={rotuloPorAcao.get(log.acao) ?? log.acao} />
                  </TableCell>
                  <TableCell>
                    {log.descricao}
                    {log.detalhes && (
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                        {log.detalhes}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>{log.enderecoIp ?? '—'}</TableCell>
                </TableRow>
              ))}
              {data?.itens.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} align="center">
                    Nenhum registro para os filtros aplicados.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
          <TablePagination
            component="div"
            count={data?.total ?? 0}
            page={pagina}
            onPageChange={(_, novaPagina) => setPagina(novaPagina)}
            rowsPerPage={tamanho}
            onRowsPerPageChange={(e) => {
              setTamanho(parseInt(e.target.value, 10))
              setPagina(0)
            }}
            rowsPerPageOptions={[10, 25, 50, 100]}
            labelRowsPerPage="Por página"
            labelDisplayedRows={({ from, to, count }) => `${from}–${to} de ${count}`}
          />
        </TableContainer>
      )}
    </>
  )
}
