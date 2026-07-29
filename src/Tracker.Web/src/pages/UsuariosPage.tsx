import { useState } from 'react'
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
} from '@mui/material'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined'
import { useUsuarios, useDesativarUsuario } from '../features/usuarios/hooks'
import { UsuarioFormDialog } from '../features/usuarios/UsuarioFormDialog'

function formatarCpf(cpf: string) {
  return cpf.length === 11 ? `${cpf.slice(0, 3)}.${cpf.slice(3, 6)}.${cpf.slice(6, 9)}-${cpf.slice(9)}` : cpf
}

export function UsuariosPage() {
  const [dialogAberto, setDialogAberto] = useState(false)
  const { data: usuarios, isLoading } = useUsuarios()
  const desativar = useDesativarUsuario()

  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h5">Usuários</Typography>
        <Button variant="contained" onClick={() => setDialogAberto(true)}>
          Novo usuário
        </Button>
      </Stack>

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
                  <TableCell>{u.telefone}</TableCell>
                  <TableCell align="center">{u.quantidadeDispositivos}</TableCell>
                  <TableCell align="center">
                    <Chip
                      size="small"
                      label={u.ativo ? 'Ativo' : 'Inativo'}
                      color={u.ativo ? 'success' : 'default'}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <IconButton
                      size="small"
                      title="Desativar"
                      disabled={!u.ativo}
                      onClick={() => desativar.mutate(u.id)}
                    >
                      <DeleteOutlineIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
              {usuarios?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} align="center">
                    Nenhum usuário cadastrado.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <UsuarioFormDialog open={dialogAberto} onClose={() => setDialogAberto(false)} />
    </>
  )
}
