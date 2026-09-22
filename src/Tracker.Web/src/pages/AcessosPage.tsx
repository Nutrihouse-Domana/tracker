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
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Alert,
  TablePagination,
} from '@mui/material'
import EditIcon from '@mui/icons-material/EditOutlined'
import KeyIcon from '@mui/icons-material/VpnKeyOutlined'
import BlockIcon from '@mui/icons-material/BlockOutlined'
import CheckIcon from '@mui/icons-material/CheckCircleOutlined'
import type { Acesso } from '../api/types'
import { useAcessos, useAlterarAtivoAcesso, useRedefinirSenhaAcesso } from '../features/acessos/hooks'
import { AcessoFormDialog } from '../features/acessos/AcessoFormDialog'
import { PERMISSOES } from '../features/auth/permissoes'
import { useMe } from '../features/auth/useAuth'
import { mensagemErroApi } from '../utils/erroApi'
import { formatarDataHora } from '../utils/formato'
import { usePaginacao } from '../utils/paginacao'

export function AcessosPage() {
  const [dialogAberto, setDialogAberto] = useState(false)
  const [acessoEmEdicao, setAcessoEmEdicao] = useState<Acesso | null>(null)
  const [acessoParaAtivo, setAcessoParaAtivo] = useState<Acesso | null>(null)
  const [acessoParaSenha, setAcessoParaSenha] = useState<Acesso | null>(null)

  const { data: acessos, isLoading } = useAcessos()
  const { data: eu } = useMe()
  const { itensPagina, total, paginacaoProps } = usePaginacao(acessos)

  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: 'space-between', alignItems: 'center' }}>
        <Box>
          <Typography variant="h5">Acessos ({total})</Typography>
          <Typography variant="body2" color="text.secondary">
            Logins da plataforma e suas permissões. (Detentores de dispositivos ficam em "Usuários".)
          </Typography>
        </Box>
        <Button variant="contained" onClick={() => setDialogAberto(true)}>
          Novo acesso
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
                <TableCell>E-mail</TableCell>
                <TableCell align="center">Permissões</TableCell>
                <TableCell>Último login</TableCell>
                <TableCell align="center">Status</TableCell>
                <TableCell align="right" />
              </TableRow>
            </TableHead>
            <TableBody>
              {itensPagina?.map((a) => (
                <TableRow key={a.id} hover>
                  <TableCell>{a.nome}</TableCell>
                  <TableCell>{a.email}</TableCell>
                  <TableCell align="center">
                    {a.permissoes.includes(PERMISSOES.acessosGerir) ? (
                      <Chip size="small" color="primary" label="Administrador" />
                    ) : (
                      <Chip size="small" variant="outlined" label={`${a.permissoes.length} permiss${a.permissoes.length === 1 ? 'ão' : 'ões'}`} />
                    )}
                  </TableCell>
                  <TableCell>{a.ultimoLoginEm ? formatarDataHora(a.ultimoLoginEm) : '—'}</TableCell>
                  <TableCell align="center">
                    <Chip
                      size="small"
                      label={a.ativo ? 'Ativo' : 'Desativado'}
                      color={a.ativo ? 'success' : 'default'}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={0.5} sx={{ justifyContent: 'flex-end' }}>
                      <IconButton size="small" title="Editar" onClick={() => setAcessoEmEdicao(a)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                      <IconButton size="small" title="Redefinir senha" onClick={() => setAcessoParaSenha(a)}>
                        <KeyIcon fontSize="small" />
                      </IconButton>
                      {/* Não oferece desativar o próprio login (auto-lockout). */}
                      {a.id !== eu?.id && (
                        <IconButton
                          size="small"
                          title={a.ativo ? 'Desativar' : 'Ativar'}
                          onClick={() => setAcessoParaAtivo(a)}
                        >
                          {a.ativo ? <BlockIcon fontSize="small" /> : <CheckIcon fontSize="small" />}
                        </IconButton>
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
              {acessos?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} align="center">
                    Nenhum acesso cadastrado.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
          {total > 0 && <TablePagination {...paginacaoProps} />}
        </TableContainer>
      )}

      <AcessoFormDialog open={dialogAberto} onClose={() => setDialogAberto(false)} />
      <AcessoFormDialog open={!!acessoEmEdicao} acesso={acessoEmEdicao} onClose={() => setAcessoEmEdicao(null)} />
      <AtivoDialog acesso={acessoParaAtivo} onClose={() => setAcessoParaAtivo(null)} />
      <SenhaDialog acesso={acessoParaSenha} onClose={() => setAcessoParaSenha(null)} />
    </>
  )
}

/** Confirma ativar/desativar um login, pedindo a senha do ADM logado. */
function AtivoDialog({ acesso, onClose }: { acesso: Acesso | null; onClose: () => void }) {
  const [senhaAdm, setSenhaAdm] = useState('')
  const alterar = useAlterarAtivoAcesso()
  const desativando = acesso?.ativo ?? false

  const handleClose = () => {
    setSenhaAdm('')
    alterar.reset()
    onClose()
  }

  const handleConfirmar = () => {
    if (!acesso) return
    alterar.mutate(
      { id: acesso.id, data: { ativo: !acesso.ativo, senhaAdm } },
      { onSuccess: handleClose },
    )
  }

  return (
    <Dialog open={!!acesso} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>{desativando ? 'Desativar acesso' : 'Ativar acesso'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {alterar.isError && (
            <Alert severity="error">{mensagemErroApi(alterar.error, 'Não foi possível alterar o status.')}</Alert>
          )}
          <Typography variant="body2">
            {desativando
              ? `Desativar o login de ${acesso?.nome}? Ele perde o acesso imediatamente.`
              : `Reativar o login de ${acesso?.nome}?`}
          </Typography>
          <TextField
            label="Sua senha (confirmação)"
            type="password"
            value={senhaAdm}
            onChange={(e) => setSenhaAdm(e.target.value)}
            required
            fullWidth
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Cancelar</Button>
        <Button
          variant="contained"
          color={desativando ? 'error' : 'primary'}
          disabled={alterar.isPending || !senhaAdm}
          onClick={handleConfirmar}
        >
          {alterar.isPending ? 'Salvando...' : desativando ? 'Desativar' : 'Ativar'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}

/** Redefine a senha de um login, pedindo a nova senha e a senha do ADM logado. */
function SenhaDialog({ acesso, onClose }: { acesso: Acesso | null; onClose: () => void }) {
  const [novaSenha, setNovaSenha] = useState('')
  const [senhaAdm, setSenhaAdm] = useState('')
  const redefinir = useRedefinirSenhaAcesso()

  const handleClose = () => {
    setNovaSenha('')
    setSenhaAdm('')
    redefinir.reset()
    onClose()
  }

  const handleConfirmar = () => {
    if (!acesso) return
    redefinir.mutate({ id: acesso.id, data: { novaSenha, senhaAdm } }, { onSuccess: handleClose })
  }

  return (
    <Dialog open={!!acesso} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>Redefinir senha</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {redefinir.isError && (
            <Alert severity="error">{mensagemErroApi(redefinir.error, 'Não foi possível redefinir a senha.')}</Alert>
          )}
          <Typography variant="body2">Nova senha para {acesso?.nome}:</Typography>
          <TextField
            label="Nova senha"
            type="password"
            value={novaSenha}
            onChange={(e) => setNovaSenha(e.target.value)}
            required
            fullWidth
            helperText="Mínimo de 10 caracteres."
          />
          <TextField
            label="Sua senha (confirmação)"
            type="password"
            value={senhaAdm}
            onChange={(e) => setSenhaAdm(e.target.value)}
            required
            fullWidth
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Cancelar</Button>
        <Button variant="contained" disabled={redefinir.isPending || !novaSenha || !senhaAdm} onClick={handleConfirmar}>
          {redefinir.isPending ? 'Salvando...' : 'Redefinir'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
