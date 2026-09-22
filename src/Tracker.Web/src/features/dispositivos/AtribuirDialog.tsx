import { useEffect, useMemo, useState } from 'react'
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Alert,
  Stack,
  TextField,
  InputAdornment,
  Checkbox,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Paper,
  TablePagination,
  Typography,
} from '@mui/material'
import SearchOutlinedIcon from '@mui/icons-material/SearchOutlined'
import type { Dispositivo } from '../../api/types'
import { useAtribuirDispositivo } from './hooks'
import { useUsuarios } from '../usuarios/hooks'
import { usePaginacao } from '../../utils/paginacao'

/** Poucos por página: a lista mora dentro do diálogo e não pode empurrar os botões para fora. */
const USUARIOS_POR_PAGINA = 5

// Cinza em vez do quase-preto do tema escuro (`background.paper` = #1E1E1E), a pedido da TI: o
// diálogo é um tom e o painel da lista o tom seguinte, para a lista não sumir dentro do diálogo.
const CINZA_DIALOGO = { light: '#F5F5F5', dark: '#2E2E2E' }
const CINZA_LISTA = { light: '#FFFFFF', dark: '#3A3A3A' }

interface Props {
  dispositivo: Dispositivo | null
  onClose: () => void
}

/** Normaliza texto para busca: minúsculo e sem acentos. */
function normalizar(texto: string) {
  return texto
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
}

export function AtribuirDialog({ dispositivo, onClose }: Props) {
  const [usuarioId, setUsuarioId] = useState('')
  const [motivo, setMotivo] = useState('')
  const [busca, setBusca] = useState('')
  const { data: usuarios } = useUsuarios()
  const atribuir = useAtribuirDispositivo()

  // Só usuários ativos, filtrados pela busca (nome/e-mail) e ordenados por nome.
  const filtrados = useMemo(() => {
    const ativos = (usuarios ?? []).filter((u) => u.ativo)
    const termo = normalizar(busca.trim())
    const encontrados = termo
      ? ativos.filter((u) => normalizar(`${u.nome} ${u.email ?? ''}`).includes(termo))
      : ativos
    return [...encontrados].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR', { numeric: true }))
  }, [usuarios, busca])

  const { itensPagina, total, setPagina, paginacaoProps } = usePaginacao(filtrados, USUARIOS_POR_PAGINA)

  // Marcar um desmarca o anterior: é escolha única, com aparência de caixa de seleção.
  const alternarSelecao = (id: string) => setUsuarioId((atual) => (atual === id ? '' : id))

  // A busca troca a lista sem necessariamente encolhê-la: volta para a primeira página.
  useEffect(() => {
    setPagina(0)
  }, [busca, setPagina])

  // Busca na lista completa: o selecionado continua visível mesmo se a busca o filtrar.
  const selecionado = usuarios?.find((u) => u.id === usuarioId)

  const handleClose = () => {
    setUsuarioId('')
    setMotivo('')
    setBusca('')
    setPagina(0)
    atribuir.reset()
    onClose()
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!dispositivo || !usuarioId) return
    atribuir.mutate(
      {
        id: dispositivo.id,
        data: { usuarioId, motivo },
      },
      { onSuccess: handleClose },
    )
  }

  return (
    <Dialog
      open={!!dispositivo}
      onClose={handleClose}
      maxWidth="sm"
      fullWidth
      slotProps={{
        paper: {
          sx: (theme) => ({
            backgroundImage: 'none',
            bgcolor: theme.palette.mode === 'dark' ? CINZA_DIALOGO.dark : CINZA_DIALOGO.light,
          }),
        },
      }}
    >
      <DialogTitle>Atribuir dispositivo</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {atribuir.isError && <Alert severity="error">Não foi possível atribuir o dispositivo.</Alert>}
            <TextField
              label="Buscar usuário"
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              fullWidth
              autoFocus
              placeholder="Nome ou e-mail"
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchOutlinedIcon fontSize="small" />
                    </InputAdornment>
                  ),
                },
              }}
            />

            <Paper
              variant="outlined"
              sx={(theme) => ({
                backgroundImage: 'none',
                bgcolor: theme.palette.mode === 'dark' ? CINZA_LISTA.dark : CINZA_LISTA.light,
              })}
            >
              {total === 0 ? (
                <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
                  Nenhum usuário encontrado.
                </Typography>
              ) : (
                <>
                  <List dense disablePadding>
                    {itensPagina?.map((u) => (
                      <ListItemButton
                        key={u.id}
                        selected={u.id === usuarioId}
                        onClick={() => alternarSelecao(u.id)}
                        // Sem realce colorido: o selecionado fica só um tom de cinza acima do fundo.
                        sx={{
                          '&.Mui-selected, &.Mui-selected:hover': { bgcolor: 'action.selected' },
                        }}
                      >
                        <ListItemIcon sx={{ minWidth: 40 }}>
                          <Checkbox
                            edge="start"
                            color="default"
                            checked={u.id === usuarioId}
                            tabIndex={-1}
                            disableRipple
                            slotProps={{ input: { 'aria-label': `Selecionar ${u.nome}` } }}
                          />
                        </ListItemIcon>
                        <ListItemText primary={u.nome} secondary={u.email || undefined} />
                      </ListItemButton>
                    ))}
                  </List>
                  <TablePagination {...paginacaoProps} rowsPerPageOptions={[]} />
                </>
              )}
            </Paper>

            <Typography variant="body2" color={selecionado ? 'text.primary' : 'text.secondary'}>
              {selecionado ? `Selecionado: ${selecionado.nome}` : 'Selecione um usuário na lista.'}
            </Typography>

            <TextField
              label="Motivo"
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              fullWidth
              placeholder="Entrega inicial, troca de equipamento, etc."
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={atribuir.isPending || !usuarioId}>
            {atribuir.isPending ? 'Atribuindo...' : 'Atribuir'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
