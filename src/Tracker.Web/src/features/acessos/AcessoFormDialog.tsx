import { useEffect, useMemo, useState } from 'react'
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Button,
  Alert,
  Stack,
  Typography,
  Checkbox,
  FormControlLabel,
  FormGroup,
  Divider,
  Box,
} from '@mui/material'
import type { Acesso } from '../../api/types'
import { useAtualizarAcesso, useCatalogoPermissoes, useCriarAcesso } from './hooks'
import { mensagemErroApi } from '../../utils/erroApi'
import { useMe } from '../auth/useAuth'
import { PERMISSOES } from '../auth/permissoes'

interface Props {
  open: boolean
  onClose: () => void
  /** Quando informado, o diálogo entra em modo de edição. */
  acesso?: Acesso | null
}

export function AcessoFormDialog({ open, onClose, acesso }: Props) {
  const edicao = !!acesso
  const [nome, setNome] = useState('')
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [senhaAdm, setSenhaAdm] = useState('')
  const [permissoes, setPermissoes] = useState<string[]>([])

  const { data: catalogo } = useCatalogoPermissoes()
  const { data: eu } = useMe()
  const criar = useCriarAcesso()
  const atualizar = useAtualizarAcesso()
  const mutacao = edicao ? atualizar : criar

  // Editando a si mesmo: não deixa desmarcar a própria permissão de gerir acessos (auto-lockout).
  const editandoProprio = edicao && acesso?.id === eu?.id

  // Agrupa o catálogo por "grupo", preservando a ordem de chegada.
  const grupos = useMemo(() => {
    const mapa = new Map<string, typeof catalogo>()
    for (const p of catalogo ?? []) {
      const atual = mapa.get(p.grupo) ?? []
      mapa.set(p.grupo, [...atual, p])
    }
    return Array.from(mapa.entries())
  }, [catalogo])

  const todasPermissoes = useMemo(() => (catalogo ?? []).map((p) => p.valor), [catalogo])
  const todasMarcadas = todasPermissoes.length > 0 && todasPermissoes.every((p) => permissoes.includes(p))

  useEffect(() => {
    if (!open) return
    setNome(acesso?.nome ?? '')
    setEmail(acesso?.email ?? '')
    setSenha('')
    setSenhaAdm('')
    setPermissoes(acesso?.permissoes ?? [])
    criar.reset()
    atualizar.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, acesso])

  const handleClose = () => {
    criar.reset()
    atualizar.reset()
    onClose()
  }

  const alternarPermissao = (valor: string) =>
    setPermissoes((atual) => (atual.includes(valor) ? atual.filter((p) => p !== valor) : [...atual, valor]))

  const alternarTodas = () =>
    setPermissoes(
      todasMarcadas
        // Ao desmarcar tudo editando a si mesmo, mantém a própria permissão de gerir acessos.
        ? (editandoProprio ? [PERMISSOES.acessosGerir] : [])
        : todasPermissoes,
    )

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (edicao) {
      atualizar.mutate(
        { id: acesso!.id, data: { nome, email, permissoes, senhaAdm } },
        { onSuccess: handleClose },
      )
    } else {
      criar.mutate({ nome, email, senha, permissoes, senhaAdm }, { onSuccess: handleClose })
    }
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>{edicao ? 'Editar acesso' : 'Novo acesso'}</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {mutacao.isError && (
              <Alert severity="error">{mensagemErroApi(mutacao.error, 'Não foi possível salvar o acesso.')}</Alert>
            )}

            <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} required fullWidth />
            <TextField
              label="E-mail (login)"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              fullWidth
            />
            {!edicao && (
              <TextField
                label="Senha do novo usuário"
                type="password"
                value={senha}
                onChange={(e) => setSenha(e.target.value)}
                required
                fullWidth
                helperText="Mínimo de 10 caracteres."
              />
            )}

            <Divider />
            <Box>
              <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                  Permissões
                </Typography>
                <FormControlLabel
                  control={<Checkbox checked={todasMarcadas} onChange={alternarTodas} />}
                  label="Administrador (tudo)"
                />
              </Stack>

              {grupos.map(([grupo, itens]) => (
                <Box key={grupo} sx={{ mt: 1 }}>
                  <Typography variant="overline" color="text.secondary">
                    {grupo}
                  </Typography>
                  <FormGroup>
                    {itens?.map((p) => (
                      <FormControlLabel
                        key={p.valor}
                        control={
                          <Checkbox
                            checked={permissoes.includes(p.valor)}
                            onChange={() => alternarPermissao(p.valor)}
                            disabled={editandoProprio && p.valor === PERMISSOES.acessosGerir}
                          />
                        }
                        label={
                          <Box>
                            <Typography variant="body2">{p.rotulo}</Typography>
                            <Typography variant="caption" color="text.secondary">
                              {editandoProprio && p.valor === PERMISSOES.acessosGerir
                                ? 'Você não pode remover a sua própria permissão de gerir acessos.'
                                : p.descricao}
                            </Typography>
                          </Box>
                        }
                      />
                    ))}
                  </FormGroup>
                </Box>
              ))}
            </Box>

            <Divider />
            <TextField
              label="Sua senha (confirmação)"
              type="password"
              value={senhaAdm}
              onChange={(e) => setSenhaAdm(e.target.value)}
              required
              fullWidth
              helperText="Confirme com a sua própria senha para salvar."
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={mutacao.isPending}>
            {mutacao.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
