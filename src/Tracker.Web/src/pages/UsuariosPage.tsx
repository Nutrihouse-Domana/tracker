import { useMemo, useState } from 'react'
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
} from '@mui/material'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined'
import EditIcon from '@mui/icons-material/EditOutlined'
import type { Usuario } from '../api/types'
import { useUsuarios, useDesativarUsuario } from '../features/usuarios/hooks'
import { UsuarioFormDialog } from '../features/usuarios/UsuarioFormDialog'
import { formatarCpf, formatarTelefone } from '../utils/formato'
import { useOrdenacao } from '../utils/ordenacao'

export function UsuariosPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const termoBusca = searchParams.get('q') ?? ''
  const [dialogAberto, setDialogAberto] = useState(false)
  const [usuarioEmEdicao, setUsuarioEmEdicao] = useState<Usuario | null>(null)
  const { data: usuariosCarregados, isLoading } = useUsuarios()
  const desativar = useDesativarUsuario()

  const usuariosFiltrados = useMemo(() => {
    if (!termoBusca.trim()) return usuariosCarregados
    const termo = termoBusca.trim().toLowerCase()
    return usuariosCarregados?.filter((u) =>
      [u.nome, u.email, u.telefone].some((campo) => campo.toLowerCase().includes(termo)),
    )
  }, [usuariosCarregados, termoBusca])

  const { ordenados: usuarios, coluna, direcao, ordenarPor } = useOrdenacao(
    usuariosFiltrados,
    'nome',
  )

  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h5">Usuários</Typography>
        <Button variant="contained" onClick={() => setDialogAberto(true)}>
          Novo usuário
        </Button>
      </Stack>

      {termoBusca && (
        <Alert severity="info" sx={{ mb: 2 }} onClose={() => setSearchParams({})}>
          Mostrando resultados para "{termoBusca}" ({usuarios?.length ?? 0})
        </Alert>
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
              {usuarios?.map((u) => (
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
                      <IconButton
                        size="small"
                        title="Desativar"
                        disabled={!u.ativo}
                        onClick={() => desativar.mutate(u.id)}
                      >
                        <DeleteOutlineIcon fontSize="small" />
                      </IconButton>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
              {usuarios?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} align="center">
                    {termoBusca
                      ? `Nenhum usuário encontrado para "${termoBusca}".`
                      : 'Nenhum usuário cadastrado.'}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <UsuarioFormDialog open={dialogAberto} onClose={() => setDialogAberto(false)} />
      <UsuarioFormDialog
        open={!!usuarioEmEdicao}
        usuario={usuarioEmEdicao}
        onClose={() => setUsuarioEmEdicao(null)}
      />
    </>
  )
}
