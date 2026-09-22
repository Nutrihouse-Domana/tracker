import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
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
  IconButton,
  CircularProgress,
  Box,
  FormControlLabel,
  Switch,
  TableSortLabel,
  TablePagination,
} from '@mui/material'
import EditIcon from '@mui/icons-material/EditOutlined'
import type { Unidade } from '../api/types'
import { useUnidades } from '../features/unidades/hooks'
import { UnidadeFormDialog } from '../features/unidades/UnidadeFormDialog'
import { useOrdenacao } from '../utils/ordenacao'
import { usePaginacao } from '../utils/paginacao'

export function UnidadesPage() {
  const navigate = useNavigate()
  const [incluirInativas, setIncluirInativas] = useState(false)
  const [dialogAberto, setDialogAberto] = useState(false)
  const [unidadeEmEdicao, setUnidadeEmEdicao] = useState<Unidade | null>(null)
  const { data: unidadesCarregadas, isLoading } = useUnidades(incluirInativas)
  const { ordenados: unidades, coluna, direcao, ordenarPor } = useOrdenacao(
    unidadesCarregadas,
    'nome',
  )
  const { itensPagina, total, paginacaoProps } = usePaginacao(unidades)

  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h5">Unidades ({total})</Typography>
        <Button variant="contained" onClick={() => setDialogAberto(true)}>
          Nova unidade
        </Button>
      </Stack>

      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Unidades que os dispositivos atendem. Ativos que pertencem ao local aparecem no inventário
        com o nome da unidade no lugar do usuário.
      </Typography>

      <FormControlLabel
        sx={{ mb: 1 }}
        control={<Switch checked={incluirInativas} onChange={(e) => setIncluirInativas(e.target.checked)} />}
        label="Mostrar inativas"
      />

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell sortDirection={coluna === 'nome' ? direcao : false}>
                  <TableSortLabel
                    active={coluna === 'nome'}
                    direction={coluna === 'nome' ? direcao : 'asc'}
                    onClick={() => ordenarPor('nome')}
                  >
                    Nome
                  </TableSortLabel>
                </TableCell>
                <TableCell sortDirection={coluna === 'codigo' ? direcao : false}>
                  <TableSortLabel
                    active={coluna === 'codigo'}
                    direction={coluna === 'codigo' ? direcao : 'asc'}
                    onClick={() => ordenarPor('codigo')}
                  >
                    Código
                  </TableSortLabel>
                </TableCell>
                <TableCell align="center" sortDirection={coluna === 'quantidadeDispositivos' ? direcao : false}>
                  <TableSortLabel
                    active={coluna === 'quantidadeDispositivos'}
                    direction={coluna === 'quantidadeDispositivos' ? direcao : 'asc'}
                    onClick={() => ordenarPor('quantidadeDispositivos')}
                  >
                    Dispositivos
                  </TableSortLabel>
                </TableCell>
                <TableCell align="center" sortDirection={coluna === 'ativa' ? direcao : false}>
                  <TableSortLabel
                    active={coluna === 'ativa'}
                    direction={coluna === 'ativa' ? direcao : 'asc'}
                    onClick={() => ordenarPor('ativa')}
                  >
                    Status
                  </TableSortLabel>
                </TableCell>
                <TableCell align="right" />
              </TableRow>
            </TableHead>
            <TableBody>
              {itensPagina?.map((u) => (
                <TableRow key={u.id} hover>
                  <TableCell>{u.nome}</TableCell>
                  <TableCell>{u.codigo ?? '—'}</TableCell>
                  <TableCell align="center">
                    <Button
                      size="small"
                      disabled={u.quantidadeDispositivos === 0}
                      onClick={() => navigate(`/dispositivos?unidadeId=${u.id}`)}
                    >
                      {u.quantidadeDispositivos}
                    </Button>
                  </TableCell>
                  <TableCell align="center">
                    <Chip size="small" label={u.ativa ? 'Ativa' : 'Inativa'} color={u.ativa ? 'success' : 'default'} />
                  </TableCell>
                  <TableCell align="right">
                    <IconButton size="small" title="Editar" onClick={() => setUnidadeEmEdicao(u)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
              {unidades?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} align="center">
                    Nenhuma unidade cadastrada.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
          {total > 0 && <TablePagination {...paginacaoProps} />}
        </TableContainer>
      )}

      <UnidadeFormDialog open={dialogAberto} onClose={() => setDialogAberto(false)} />
      <UnidadeFormDialog
        open={!!unidadeEmEdicao}
        unidade={unidadeEmEdicao}
        onClose={() => setUnidadeEmEdicao(null)}
      />
    </>
  )
}
