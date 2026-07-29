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
} from '@mui/material'
import type { TipoDispositivo, TipoPeriferico } from '../../api/types'
import { useCriarNotebook, useCriarPeriferico, useCriarTelefone } from './hooks'

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

  const criarNotebook = useCriarNotebook()
  const criarTelefone = useCriarTelefone()
  const criarPeriferico = useCriarPeriferico()

  const mutacaoAtiva = tipo === 'Notebook' ? criarNotebook : tipo === 'Telefone' ? criarTelefone : criarPeriferico

  const handleClose = () => {
    setNumeroSerie('')
    setAssetTag('')
    setHostname('')
    setSistemaOperacional('')
    setImei('')
    setObservacoes('')
    criarNotebook.reset()
    criarTelefone.reset()
    criarPeriferico.reset()
    onClose()
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const dataAquisicao = new Date().toISOString()

    if (tipo === 'Notebook') {
      criarNotebook.mutate(
        { numeroSerie, assetTag, hostname, sistemaOperacional, dataAquisicao, observacoes },
        { onSuccess: handleClose },
      )
    } else if (tipo === 'Telefone') {
      criarTelefone.mutate(
        { numeroSerie, assetTag, hostname, sistemaOperacional, imei, dataAquisicao, observacoes },
        { onSuccess: handleClose },
      )
    } else {
      criarPeriferico.mutate(
        { numeroSerie, assetTag, tipoPeriferico, dataAquisicao, observacoes },
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
                Não foi possível criar o dispositivo. Verifique se o número de série/patrimônio já existem.
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
              <ToggleButton value="Telefone">Telefone</ToggleButton>
              <ToggleButton value="Periferico">Periférico</ToggleButton>
            </ToggleButtonGroup>

            <TextField
              label="Número de série"
              value={numeroSerie}
              onChange={(e) => setNumeroSerie(e.target.value)}
              required
              fullWidth
            />
            <TextField
              label="Patrimônio"
              value={assetTag}
              onChange={(e) => setAssetTag(e.target.value)}
              required
              fullWidth
            />

            {(tipo === 'Notebook' || tipo === 'Telefone') && (
              <>
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

            {tipo === 'Telefone' && (
              <TextField
                label="IMEI"
                value={imei}
                onChange={(e) => setImei(e.target.value)}
                required
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
