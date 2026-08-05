import { useEffect, useMemo, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import {
  Box,
  InputBase,
  IconButton,
  Popper,
  Paper,
  List,
  ListItemButton,
  ListItemText,
  ListSubheader,
  Chip,
  CircularProgress,
  Typography,
  ClickAwayListener,
  Stack,
} from '@mui/material'
import SearchIcon from '@mui/icons-material/SearchOutlined'
import CloseIcon from '@mui/icons-material/CloseOutlined'
import { useBuscaGlobal } from './hooks'
import { corStatus, rotuloStatus } from '../dispositivos/status'
import type { BuscaDispositivo, BuscaUsuario } from '../../api/types'

type Resultado =
  | { kind: 'dispositivo'; item: BuscaDispositivo }
  | { kind: 'usuario'; item: BuscaUsuario }

/** Texto secundário do dispositivo: só os campos preenchidos, separados por • */
function detalheDispositivo(d: BuscaDispositivo) {
  return [d.assetTag, d.numeroSerie, d.imei, d.usuarioAtualNome].filter(Boolean).join(' • ')
}

/**
 * Busca global do topo: consulta a API conforme se digita e abre um painel com dispositivos
 * e usuários encontrados. Enter sem selecionar nada abre a lista de dispositivos filtrada.
 */
export function BarraDeBusca() {
  const navigate = useNavigate()
  const location = useLocation()
  const [termo, setTermo] = useState('')
  const [aberto, setAberto] = useState(false)
  const [indiceAtivo, setIndiceAtivo] = useState(-1)
  const ancoraRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const { data, isFetching, habilitado } = useBuscaGlobal(termo)

  // Mantém o campo em sincronia com o ?q= da URL (recarregar a página, voltar/avançar).
  const qDaUrl = new URLSearchParams(location.search).get('q') ?? ''
  useEffect(() => {
    if (qDaUrl) setTermo(qDaUrl)
  }, [qDaUrl])

  const resultados = useMemo<Resultado[]>(
    () => [
      ...(data?.dispositivos ?? []).map((item) => ({ kind: 'dispositivo' as const, item })),
      ...(data?.usuarios ?? []).map((item) => ({ kind: 'usuario' as const, item })),
    ],
    [data],
  )

  const fechar = () => {
    setAberto(false)
    setIndiceAtivo(-1)
  }

  const abrirResultado = (resultado: Resultado) => {
    fechar()
    inputRef.current?.blur()
    if (resultado.kind === 'dispositivo') navigate(`/dispositivos/${resultado.item.id}`)
    else navigate(`/usuarios?q=${encodeURIComponent(resultado.item.nome)}`)
  }

  const submeter = (e: React.FormEvent) => {
    e.preventDefault()
    if (indiceAtivo >= 0 && resultados[indiceAtivo]) {
      abrirResultado(resultados[indiceAtivo])
      return
    }
    if (!termo.trim()) return
    fechar()
    navigate(`/dispositivos?q=${encodeURIComponent(termo.trim())}`)
  }

  const aoTeclar = (e: React.KeyboardEvent) => {
    if (e.key === 'Escape') {
      fechar()
      return
    }
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      if (!resultados.length) return
      e.preventDefault()
      setAberto(true)
      setIndiceAtivo((atual) => {
        const passo = e.key === 'ArrowDown' ? 1 : -1
        return (atual + passo + resultados.length) % resultados.length
      })
    }
  }

  const limpar = () => {
    setTermo('')
    fechar()
    inputRef.current?.focus()
  }

  const mostrarPainel = aberto && habilitado
  const semResultados = mostrarPainel && !isFetching && resultados.length === 0

  return (
    <ClickAwayListener onClickAway={fechar}>
      <Box sx={{ flexGrow: 1, maxWidth: { xs: 'none', md: 360 } }}>
        <Box
          component="form"
          onSubmit={submeter}
          ref={ancoraRef}
          sx={{
            display: 'flex',
            alignItems: 'center',
            bgcolor: 'grey.100',
            borderRadius: 999,
            px: 2,
            py: 0.5,
          }}
        >
          <InputBase
            inputRef={inputRef}
            placeholder="Buscar dispositivos ou usuários..."
            value={termo}
            onChange={(e) => {
              setTermo(e.target.value)
              setAberto(true)
              setIndiceAtivo(-1)
            }}
            onFocus={() => setAberto(true)}
            onKeyDown={aoTeclar}
            sx={{ flexGrow: 1, fontSize: 14 }}
          />
          {isFetching && <CircularProgress size={14} sx={{ mr: 1 }} />}
          {termo ? (
            <IconButton size="small" onClick={limpar} title="Limpar busca">
              <CloseIcon fontSize="small" />
            </IconButton>
          ) : (
            <SearchIcon fontSize="small" color="action" />
          )}
        </Box>

        <Popper
          open={mostrarPainel}
          anchorEl={ancoraRef.current}
          placement="bottom-start"
          style={{ zIndex: 1300, width: ancoraRef.current?.clientWidth }}
        >
          <Paper elevation={4} sx={{ mt: 0.5, maxHeight: 420, overflowY: 'auto' }}>
            {semResultados ? (
              <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
                Nada encontrado para "{termo.trim()}".
              </Typography>
            ) : (
              <List dense disablePadding>
                {!!data?.dispositivos.length && <ListSubheader>Dispositivos</ListSubheader>}
                {data?.dispositivos.map((d, i) => (
                  <ListItemButton
                    key={d.id}
                    selected={indiceAtivo === i}
                    onClick={() => abrirResultado({ kind: 'dispositivo', item: d })}
                  >
                    <ListItemText
                      primary={
                        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                          <span>{d.hostname ?? d.numeroSerie}</span>
                          <Chip
                            size="small"
                            label={rotuloStatus[d.status] ?? d.status}
                            color={corStatus[d.status] ?? 'default'}
                          />
                        </Stack>
                      }
                      secondary={detalheDispositivo(d)}
                    />
                  </ListItemButton>
                ))}

                {!!data?.usuarios.length && <ListSubheader>Usuários</ListSubheader>}
                {data?.usuarios.map((u, i) => (
                  <ListItemButton
                    key={u.id}
                    selected={indiceAtivo === (data?.dispositivos.length ?? 0) + i}
                    onClick={() => abrirResultado({ kind: 'usuario', item: u })}
                  >
                    <ListItemText
                      primary={u.nome}
                      secondary={`${u.email} • ${u.quantidadeDispositivos} dispositivo(s)${u.ativo ? '' : ' • inativo'}`}
                    />
                  </ListItemButton>
                ))}
              </List>
            )}
          </Paper>
        </Popper>
      </Box>
    </ClickAwayListener>
  )
}
