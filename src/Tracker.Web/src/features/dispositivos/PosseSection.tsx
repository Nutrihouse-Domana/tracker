import { useEffect, useState } from 'react'
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  FormControlLabel,
  MenuItem,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material'
import type { Dispositivo } from '../../api/types'
import { useDefinirPosseDispositivo, useDefinirUnidadeDispositivo } from './hooks'
import { useUnidades } from '../unidades/hooks'
import { mensagemErroApi } from '../../utils/erroApi'
import { usePermissoes, PERMISSOES } from '../auth/permissoes'
import { tipoAceitaDetentor } from './status'

interface Props {
  dispositivo: Dispositivo
}

/**
 * Unidade que o dispositivo atende e a quem ele pertence: a um usuário nominal (fluxo normal de
 * atribuição) ou ao próprio local, caso em que a unidade aparece no lugar do usuário.
 */
export function PosseSection({ dispositivo }: Props) {
  const { data: unidades } = useUnidades()
  const { tem } = usePermissoes()
  const podeCadastrar = tem(PERMISSOES.dispositivosCadastrar)
  const podeMovimentar = tem(PERMISSOES.dispositivosMovimentar)
  const definirUnidade = useDefinirUnidadeDispositivo()
  const [dialogAberto, setDialogAberto] = useState(false)

  // Ao trocar para "pertence ao local" é preciso escolher a unidade na hora: sem ela não há o que
  // exibir no lugar do usuário.
  const [unidadeIdConfirmacao, setUnidadeIdConfirmacao] = useState('')
  const [senha, setSenha] = useState('')
  const [motivo, setMotivo] = useState('')
  const definirPosse = useDefinirPosseDispositivo()

  const pertenceAUsuario = dispositivo.pertenceAUsuario
  const novoValor = !pertenceAUsuario

  useEffect(() => {
    if (!dialogAberto) return
    setUnidadeIdConfirmacao(dispositivo.unidadeId ?? '')
    setSenha('')
    setMotivo('')
    definirPosse.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dialogAberto])

  const handleConfirmar = (e: React.FormEvent) => {
    e.preventDefault()
    definirPosse.mutate(
      {
        id: dispositivo.id,
        data: {
          pertenceAUsuario: novoValor,
          unidadeId: unidadeIdConfirmacao || null,
          senha,
          motivo: motivo.trim() || null,
        },
      },
      { onSuccess: () => setDialogAberto(false) },
    )
  }

  return (
    <Stack spacing={2}>
      <Stack spacing={0.5}>
        <TextField
          select
          label="Unidade que o dispositivo atende"
          value={dispositivo.unidadeId ?? ''}
          onChange={(e) => definirUnidade.mutate({ id: dispositivo.id, unidadeId: e.target.value || null })}
          disabled={!podeCadastrar || definirUnidade.isPending}
          helperText={!podeCadastrar ? 'Somente leitura.' : undefined}
          fullWidth
          sx={{ maxWidth: 360 }}
        >
          <MenuItem value="">— Sem unidade —</MenuItem>
          {unidades?.map((u) => (
            <MenuItem key={u.id} value={u.id}>
              {u.codigo ? `${u.nome} (${u.codigo})` : u.nome}
            </MenuItem>
          ))}
        </TextField>
        {definirUnidade.isError && (
          <Alert severity="error">
            {mensagemErroApi(definirUnidade.error, 'Não foi possível alterar a unidade.')}
          </Alert>
        )}
        {!unidades?.length && (
          <Typography variant="caption" color="text.secondary">
            Nenhuma unidade cadastrada ainda — cadastre em Unidades.
          </Typography>
        )}
      </Stack>

      {/* Impressora é sempre do local: sem interruptor (o backend recusa "pertence a usuário"). */}
      {!tipoAceitaDetentor(dispositivo.tipo) ? (
        <Stack spacing={0.5}>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            Pertence ao local (unidade)
          </Typography>
          <Typography variant="caption" color="text.secondary">
            Impressoras são sempre da unidade: não são atribuídas a usuários nem têm termo de uso.
          </Typography>
        </Stack>
      ) : (
        <Stack spacing={0.5}>
          {podeMovimentar ? (
            <FormControlLabel
              control={<Switch checked={pertenceAUsuario} onChange={() => setDialogAberto(true)} />}
              label={pertenceAUsuario ? 'Pertence a um usuário' : 'Pertence ao local (unidade)'}
            />
          ) : (
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {pertenceAUsuario ? 'Pertence a um usuário' : 'Pertence ao local (unidade)'}
            </Typography>
          )}
          <Typography variant="caption" color="text.secondary">
            {pertenceAUsuario
              ? 'O ativo tem um detentor nominal: segue o fluxo de atribuição e termo de uso.'
              : `O ativo é da unidade${
                  dispositivo.unidadeNome ? ` ${dispositivo.unidadeNome}` : ''
                } e aparece com o nome dela no lugar do usuário.`}
          </Typography>
        </Stack>
      )}

      <Dialog open={dialogAberto} onClose={() => setDialogAberto(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{novoValor ? 'Passar a pertencer a um usuário' : 'Passar a pertencer ao local'}</DialogTitle>
        <form onSubmit={handleConfirmar}>
          <DialogContent>
            <Stack spacing={2}>
              <DialogContentText>
                {novoValor
                  ? 'O dispositivo volta ao fluxo normal e poderá ser atribuído a um usuário.'
                  : 'O detentor atual será liberado (com registro no histórico) e a unidade passa a aparecer no lugar do usuário.'}
              </DialogContentText>

              {definirPosse.isError && (
                <Alert severity="error">
                  {mensagemErroApi(definirPosse.error, 'Não foi possível alterar a posse do dispositivo.')}
                </Alert>
              )}

              {!novoValor && (
                <TextField
                  select
                  label="Unidade"
                  value={unidadeIdConfirmacao}
                  onChange={(e) => setUnidadeIdConfirmacao(e.target.value)}
                  required
                  fullWidth
                >
                  {unidades?.map((u) => (
                    <MenuItem key={u.id} value={u.id}>
                      {u.codigo ? `${u.nome} (${u.codigo})` : u.nome}
                    </MenuItem>
                  ))}
                </TextField>
              )}

              <TextField
                label="Motivo (opcional)"
                value={motivo}
                onChange={(e) => setMotivo(e.target.value)}
                fullWidth
                slotProps={{ htmlInput: { maxLength: 400 } }}
              />
              <TextField
                label="Sua senha do Tracker"
                type="password"
                value={senha}
                onChange={(e) => setSenha(e.target.value)}
                required
                fullWidth
                autoComplete="current-password"
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDialogAberto(false)}>Cancelar</Button>
            <Button
              type="submit"
              variant="contained"
              disabled={definirPosse.isPending || !senha || (!novoValor && !unidadeIdConfirmacao)}
            >
              {definirPosse.isPending ? 'Salvando...' : 'Confirmar'}
            </Button>
          </DialogActions>
        </form>
      </Dialog>
    </Stack>
  )
}
