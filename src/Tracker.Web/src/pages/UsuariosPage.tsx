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
} from '@mui/material'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined'
import EditIcon from '@mui/icons-material/EditOutlined'
import type { Usuario } from '../api/types'
import { useUsuarios, useDesativarUsuario } from '../features/usuarios/hooks'
import { UsuarioFormDialog } from '../features/usuarios/UsuarioFormDialog'
import { formatarCpf, formatarTelefone } from '../utils/formato'

export function UsuariosPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const termoBusca = searchParams.get('q') ?? ''
  const [dialogAberto, setDialogAberto] = useState(false)
  const [usuarioEmEdicao, setUsuarioEmEdicao] = useState<Usuario | null>(null)
  const { data: usuariosCarregados, isLoading } = useUsuarios()
  const desativar = useDesativarUsuario()

  const usuarios = useMemo(() => {
    if (!termoBusca.trim()) return usuariosCarregados
    const termo = termoBusca.trim().toLowerCase()
    return usuariosCarregados?.filter((u) =>
      [u.nome, u.email, u.telefone].some((campo) => campo.toLowerCase().includes(termo)),
    )
  }, [usuariosCarregados, termoBusca])

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
                <TableCell>Nome</TableCell>
                <TableCell>CPF</TableCell>
                <TableCell>Email</TableCell>
                <TableCell>Telefone</TableCell>
                <TableCell align="center">Dispositivos</TableCell>
                <TableCell align="center">Status</TableCell>
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
