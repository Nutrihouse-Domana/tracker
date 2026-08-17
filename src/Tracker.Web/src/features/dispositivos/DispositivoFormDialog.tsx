import { useState } from 'react'
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Button,
  Alert,
  Stack,
  ToggleButtonGroup,
  ToggleButton,
  MenuItem,
  FormControlLabel,
  Switch,
  Typography,
} from '@mui/material'
import type { TipoDispositivo, TipoPeriferico } from '../../api/types'
import { useCriarMobile, useCriarNotebook, useCriarPeriferico } from './hooks'
import { useUnidades } from '../unidades/hooks'
import { mensagemErroApi } from '../../utils/erroApi'

interface Props {
  open: boolean
  onClose: () => void
}

const tiposPeriferico: TipoPeriferico[] = ['Mouse', 'Teclado', 'Monitor', 'Dock', 'Headset', 'Outro']

export function DispositivoFormDialog({ open, onClose }: Props) {
  const [tipo, setTipo] = useState<TipoDispositivo>('Notebook')
  const [numeroSerie, setNumeroSerie] = useState('')
  const [assetTag, setAssetTag] = useState('')
  const [hostname, setHostname] = useState('')
  const [sistemaOperacional, setSistemaOperacional] = useState('')
  const [imei, setImei] = useState('')
  const [tipoPeriferico, setTipoPeriferico] = useState<TipoPeriferico>('Mouse')
  const [observacoes, setObservacoes] = useState('')
  const [unidadeId, setUnidadeId] = useState('')
  const [pertenceAUsuario, setPertenceAUsuario] = useState(true)

  const { data: unidades } = useUnidades()
  const criarNotebook = useCriarNotebook()
  const criarMobile = useCriarMobile()
  const criarPeriferico = useCriarPeriferico()

  /** Notebook e mobile têm agente instalado; periférico é inventário manual. */
  const comAgente = tipo !== 'Periferico'

  const mutacoes = {
    Notebook: criarNotebook,
    Mobile: criarMobile,
    Periferico: criarPeriferico,
  } as const

  const mutacaoAtiva = mutacoes[tipo]

  const handleClose = () => {
    setNumeroSerie('')
    setAssetTag('')
    setHostname('')
    setSistemaOperacional('')
    setImei('')
    setObservacoes('')
    setUnidadeId('')
    setPertenceAUsuario(true)
    criarNotebook.reset()
    criarMobile.reset()
    criarPeriferico.reset()
    onClose()
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const dataAquisicao = new Date().toISOString()
    const posse = { unidadeId: unidadeId || null, pertenceAUsuario }

    if (tipo === 'Notebook') {
      criarNotebook.mutate(
        { numeroSerie, assetTag, hostname, sistemaOperacional, dataAquisicao, observacoes, ...posse },
        { onSuccess: handleClose },
      )
    } else if (tipo === 'Mobile') {
      criarMobile.mutate(
        {
          numeroSerie,
          assetTag,
          hostname,
          sistemaOperacional,
          // Vazio significa aparelho só-Wi-Fi, que não tem IMEI.
          imei: imei.trim() || null,
          dataAquisicao,
          observacoes,
          ...posse,
        },
        { onSuccess: handleClose },
      )
    } else {
      criarPeriferico.mutate(
        { numeroSerie, tipoPeriferico, dataAquisicao, observacoes, ...posse },
        { onSuccess: handleClose },
      )
    }
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>Novo dispositivo</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {mutacaoAtiva.isError && (
              <Alert severity="error">
                {mensagemErroApi(
                  mutacaoAtiva.error,
                  'Não foi possível criar o dispositivo. Verifique se o número de série ou o patrimônio já existem.',
                )}
              </Alert>
            )}

            <ToggleButtonGroup
              color="primary"
              exclusive
              fullWidth
              value={tipo}
              onChange={(_, v) => v && setTipo(v)}
            >
              <ToggleButton value="Notebook">Notebook</ToggleButton>
              <ToggleButton value="Mobile">Mobile</ToggleButton>
              <ToggleButton value="Periferico">Periférico</ToggleButton>
            </ToggleButtonGroup>

            <TextField
              label="Número de série"
              value={numeroSerie}
              onChange={(e) => setNumeroSerie(e.target.value)}
              required
              fullWidth
            />
            {/* Periférico não recebe etiqueta de patrimônio. */}
            {comAgente && (
              <>
                <TextField
                  label="Patrimônio"
                  value={assetTag}
                  onChange={(e) => setAssetTag(e.target.value)}
                  required
                  fullWidth
                />
                <TextField
                  label="Hostname"
                  value={hostname}
                  onChange={(e) => setHostname(e.target.value)}
                  required
                  fullWidth
                />
                <TextField
                  label="Sistema operacional"
                  value={sistemaOperacional}
                  onChange={(e) => setSistemaOperacional(e.target.value)}
                  required
                  fullWidth
                  placeholder={tipo === 'Notebook' ? 'Windows 11 Pro' : 'Android 14'}
                />
              </>
            )}

            {tipo === 'Mobile' && (
              <TextField
                // Opcional: tablets só-Wi-Fi não têm IMEI.
                label="IMEI (opcional em aparelhos só Wi-Fi)"
                value={imei}
                onChange={(e) => setImei(e.target.value)}
                fullWidth
                slotProps={{ htmlInput: { maxLength: 15 } }}
              />
            )}

            {tipo === 'Periferico' && (
              <TextField
                select
                label="Tipo de periférico"
                value={tipoPeriferico}
                onChange={(e) => setTipoPeriferico(e.target.value as TipoPeriferico)}
                fullWidth
              >
                {tiposPeriferico.map((t) => (
                  <MenuItem key={t} value={t}>
                    {t}
                  </MenuItem>
                ))}
              </TextField>
            )}

            <TextField
              select
              label="Unidade que o dispositivo atende"
              value={unidadeId}
              onChange={(e) => setUnidadeId(e.target.value)}
              // Obrigatória quando o ativo é do local: é o nome dela que aparece no lugar do usuário.
              required={!pertenceAUsuario}
              fullWidth
            >
              <MenuItem value="">— Sem unidade —</MenuItem>
              {unidades?.map((u) => (
                <MenuItem key={u.id} value={u.id}>
                  {u.codigo ? `${u.nome} (${u.codigo})` : u.nome}
                </MenuItem>
              ))}
            </TextField>

            <Stack spacing={0.5}>
              <FormControlLabel
                control={
                  <Switch
                    checked={pertenceAUsuario}
                    onChange={(e) => setPertenceAUsuario(e.target.checked)}
                  />
                }
                label={pertenceAUsuario ? 'Pertence a um usuário' : 'Pertence ao local (unidade)'}
              />
              <Typography variant="caption" color="text.secondary">
                {pertenceAUsuario
                  ? 'Será atribuído a um usuário pelo fluxo normal.'
                  : 'Fica lotado na unidade, que aparece no lugar do usuário.'}
              </Typography>
            </Stack>

            <TextField
              label="Observações"
              value={observacoes}
              onChange={(e) => setObservacoes(e.target.value)}
              fullWidth
              multiline
              minRows={2}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={mutacaoAtiva.isPending}>
            {mutacaoAtiva.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
