import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
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
  Alert,
  TableSortLabel,
  TablePagination,
  FormControlLabel,
  Switch,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogContentText,
  DialogActions,
} from '@mui/material'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined'
import EditIcon from '@mui/icons-material/EditOutlined'
import RestoreIcon from '@mui/icons-material/RestoreOutlined'
import type { Usuario } from '../api/types'
import { useUsuarios, useDesativarUsuario, useReativarUsuario } from '../features/usuarios/hooks'
import { UsuarioFormDialog } from '../features/usuarios/UsuarioFormDialog'
import { formatarCpf, formatarTelefone } from '../utils/formato'
import { useOrdenacao } from '../utils/ordenacao'
import { usePaginacao } from '../utils/paginacao'
import { mensagemErroApi } from '../utils/erroApi'

export function UsuariosPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const termoBusca = searchParams.get('q') ?? ''
  const [dialogAberto, setDialogAberto] = useState(false)
  const [usuarioEmEdicao, setUsuarioEmEdicao] = useState<Usuario | null>(null)
  const [mostrarInativos, setMostrarInativos] = useState(false)
  const [usuarioADesativar, setUsuarioADesativar] = useState<Usuario | null>(null)
  // Carrega SEMPRE com os inativos e filtra no cliente: sem isso o detentor desativado sumia da
  // tela e não havia como reativá-lo.
  const { data: usuariosCarregados, isLoading, isError, error } = useUsuarios(true)
  const desativar = useDesativarUsuario()
  const reativar = useReativarUsuario()

  const usuariosFiltrados = useMemo(() => {
    const visiveis = mostrarInativos ? usuariosCarregados : usuariosCarregados?.filter((u) => u.ativo)
    if (!termoBusca.trim()) return visiveis
    const termo = termoBusca.trim().toLowerCase()
    return visiveis?.filter((u) =>
      [u.nome, u.email, u.telefone].some((campo) => campo.toLowerCase().includes(termo)),
    )
  }, [usuariosCarregados, termoBusca, mostrarInativos])

  const { ordenados: usuarios, coluna, direcao, ordenarPor } = useOrdenacao(
    usuariosFiltrados,
    'nome',
  )
  const { itensPagina, total, setPagina, paginacaoProps } = usePaginacao(usuarios)

  // Busca/filtro nova começa da primeira página (a lista pode mudar sem encolher).
  useEffect(() => {
    setPagina(0)
  }, [termoBusca, mostrarInativos, setPagina])

  const fecharConfirmacao = () => {
    desativar.reset()
    setUsuarioADesativar(null)
  }

  const confirmarDesativacao = () => {
    if (!usuarioADesativar) return
    desativar.mutate(usuarioADesativar.id, { onSuccess: fecharConfirmacao })
  }

  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h5">Usuários ({total})</Typography>
        <Button variant="contained" onClick={() => setDialogAberto(true)}>
          Novo usuário
        </Button>
      </Stack>

      {termoBusca && (
        <Alert severity="info" sx={{ mb: 2 }} onClose={() => setSearchParams({})}>
          Mostrando resultados para "{termoBusca}" ({usuarios?.length ?? 0})
        </Alert>
      )}

      <FormControlLabel
        sx={{ mb: 1 }}
        control={<Switch checked={mostrarInativos} onChange={(e) => setMostrarInativos(e.target.checked)} />}
        label="Mostrar inativos"
      />

      {reativar.isError && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => reativar.reset()}>
          {mensagemErroApi(reativar.error, 'Não foi possível reativar o usuário.')}
        </Alert>
      )}

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      ) : isError ? (
        <Alert severity="error">{mensagemErroApi(error, 'Não foi possível carregar os usuários.')}</Alert>
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
                <TableCell sortDirection={coluna === 'cpf' ? direcao : false}>
                  <TableSortLabel
                    active={coluna === 'cpf'}
                    direction={coluna === 'cpf' ? direcao : 'asc'}
                    onClick={() => ordenarPor('cpf')}
                  >
                    CPF
                  </TableSortLabel>
                </TableCell>
                <TableCell sortDirection={coluna === 'email' ? direcao : false}>
                  <TableSortLabel
                    active={coluna === 'email'}
                    direction={coluna === 'email' ? direcao : 'asc'}
                    onClick={() => ordenarPor('email')}
                  >
                    Email
                  </TableSortLabel>
                </TableCell>
                <TableCell sortDirection={coluna === 'telefone' ? direcao : false}>
                  <TableSortLabel
                    active={coluna === 'telefone'}
                    direction={coluna === 'telefone' ? direcao : 'asc'}
                    onClick={() => ordenarPor('telefone')}
                  >
                    Telefone
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
                <TableCell align="center" sortDirection={coluna === 'ativo' ? direcao : false}>
                  <TableSortLabel
                    active={coluna === 'ativo'}
                    direction={coluna === 'ativo' ? direcao : 'asc'}
                    onClick={() => ordenarPor('ativo')}
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
                  <TableCell>{formatarCpf(u.cpf)}</TableCell>
                  <TableCell>{u.email}</TableCell>
                  <TableCell>{formatarTelefone(u.telefone)}</TableCell>
                  <TableCell align="center">{u.quantidadeDispositivos}</TableCell>
                  <TableCell align="center">
                    <Chip
                      size="small"
                      label={u.ativo ? 'Ativo' : 'Inativo'}
                      color={u.ativo ? 'success' : 'default'}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={0.5} sx={{ justifyContent: 'flex-end' }}>
                      <IconButton size="small" title="Editar" onClick={() => setUsuarioEmEdicao(u)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                      {u.ativo ? (
                        <IconButton size="small" title="Desativar" onClick={() => setUsuarioADesativar(u)}>
                          <DeleteOutlineIcon fontSize="small" />
                        </IconButton>
                      ) : (
                        <IconButton
                          size="small"
                          title="Reativar"
                          disabled={reativar.isPending}
                          onClick={() => reativar.mutate(u)}
                        >
                          <RestoreIcon fontSize="small" />
                        </IconButton>
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
              {usuarios?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} align="center">
                    {termoBusca
                      ? `Nenhum usuário encontrado para "${termoBusca}".`
                      : mostrarInativos
                        ? 'Nenhum usuário cadastrado.'
                        : 'Nenhum usuário ativo. Ligue "Mostrar inativos" para ver os desativados.'}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
          {total > 0 && <TablePagination {...paginacaoProps} />}
        </TableContainer>
      )}

      <Dialog open={!!usuarioADesativar} onClose={fecharConfirmacao} maxWidth="xs" fullWidth>
        <DialogTitle>Desativar usuário</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <DialogContentText>
              Desativar <strong>{usuarioADesativar?.nome}</strong>? Ele deixa de aparecer para
              atribuição de dispositivos. Dá para reativar depois (ligue "Mostrar inativos").
            </DialogContentText>
            {desativar.isError && (
              <Alert severity="error">
                {mensagemErroApi(desativar.error, 'Não foi possível desativar o usuário.')}
              </Alert>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={fecharConfirmacao}>Cancelar</Button>
          <Button
            variant="contained"
            color="error"
            onClick={confirmarDesativacao}
            disabled={desativar.isPending}
          >
            {desativar.isPending ? 'Desativando...' : 'Desativar'}
          </Button>
        </DialogActions>
      </Dialog>

      <UsuarioFormDialog open={dialogAberto} onClose={() => setDialogAberto(false)} />
      <UsuarioFormDialog
        open={!!usuarioEmEdicao}
        usuario={usuarioEmEdicao}
        onClose={() => setUsuarioEmEdicao(null)}
      />
    </>
  )
}
