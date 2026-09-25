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
import type { ConexaoImpressora, TipoDispositivo, TipoImpressora, TipoPeriferico } from '../../api/types'
import { useCriarImpressora, useCriarMobile, useCriarNotebook, useCriarPeriferico } from './hooks'
import { useUnidades } from '../unidades/hooks'
import { mensagemErroApi } from '../../utils/erroApi'
import { enderecoRedePareceValido, rotuloTipoImpressora, tiposImpressora } from './impressora'

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
  // --- Impressora ---
  const [tipoImpressora, setTipoImpressora] = useState<TipoImpressora>('Laser')
  const [fabricante, setFabricante] = useState('')
  const [modelo, setModelo] = useState('')
  const [colorida, setColorida] = useState(false)
  const [alugada, setAlugada] = useState(false)
  const [conexao, setConexao] = useState<ConexaoImpressora>('Rede')
  const [enderecoRede, setEnderecoRede] = useState('')
  const [macAddress, setMacAddress] = useState('')
  const [monitorarSnmp, setMonitorarSnmp] = useState(false)

  const { data: unidades } = useUnidades()
  const criarNotebook = useCriarNotebook()
  const criarMobile = useCriarMobile()
  const criarPeriferico = useCriarPeriferico()
  const criarImpressora = useCriarImpressora()

  /** Notebook e mobile têm agente instalado; periférico e impressora são inventário manual. */
  const comAgente = tipo === 'Notebook' || tipo === 'Mobile'
  const ehImpressora = tipo === 'Impressora'
  // Impressora é sempre do local: a unidade é obrigatória e o interruptor de posse some.
  const doLocal = ehImpressora || !pertenceAUsuario
  const enderecoInvalido = ehImpressora && conexao === 'Rede' && enderecoRede.trim() !== '' && !enderecoRedePareceValido(enderecoRede)

  const mutacoes = {
    Notebook: criarNotebook,
    Mobile: criarMobile,
    Periferico: criarPeriferico,
    Impressora: criarImpressora,
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
    setTipoImpressora('Laser')
    setFabricante('')
    setModelo('')
    setColorida(false)
    setAlugada(false)
    setConexao('Rede')
    setEnderecoRede('')
    setMacAddress('')
    setMonitorarSnmp(false)
    criarNotebook.reset()
    criarMobile.reset()
    criarPeriferico.reset()
    criarImpressora.reset()
    onClose()
  }

  /** USB não é alcançável pelo servidor: desliga o SNMP junto (o backend força o mesmo). */
  const trocarConexao = (nova: ConexaoImpressora) => {
    setConexao(nova)
    if (nova === 'Usb') setMonitorarSnmp(false)
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
    } else if (tipo === 'Impressora') {
      criarImpressora.mutate(
        {
          numeroSerie,
          assetTag: assetTag.trim() || null,
          tipoImpressora,
          fabricante: fabricante.trim() || null,
          modelo: modelo.trim() || null,
          colorida,
          alugada,
          conexao,
          enderecoRede: conexao === 'Rede' ? enderecoRede.trim() || null : null,
          macAddress: macAddress.trim() || null,
          monitorarSnmp: conexao === 'Rede' && monitorarSnmp,
          dataAquisicao,
          observacoes,
          unidadeId,
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
              size="small"
              value={tipo}
              onChange={(_, v) => v && setTipo(v)}
            >
              <ToggleButton value="Notebook">Notebook</ToggleButton>
              <ToggleButton value="Mobile">Mobile</ToggleButton>
              <ToggleButton value="Periferico">Periférico</ToggleButton>
              <ToggleButton value="Impressora">Impressora</ToggleButton>
            </ToggleButtonGroup>

            <TextField
              label="Número de série"
              value={numeroSerie}
              onChange={(e) => setNumeroSerie(e.target.value)}
              required
              fullWidth
            />
            {/* Periférico não recebe etiqueta de patrimônio; na impressora ela é opcional
                (impressora alugada costuma vir sem etiqueta da empresa). */}
            {(comAgente || ehImpressora) && (
              <TextField
                label={ehImpressora ? 'Patrimônio (opcional)' : 'Patrimônio'}
                value={assetTag}
                onChange={(e) => setAssetTag(e.target.value)}
                required={comAgente}
                fullWidth
              />
            )}
            {comAgente && (
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

            {ehImpressora && (
              <>
                <TextField
                  select
                  label="Tipo de impressora"
                  value={tipoImpressora}
                  onChange={(e) => setTipoImpressora(e.target.value as TipoImpressora)}
                  fullWidth
                >
                  {tiposImpressora.map((t) => (
                    <MenuItem key={t} value={t}>
                      {rotuloTipoImpressora[t]}
                    </MenuItem>
                  ))}
                </TextField>
                <Stack direction="row" spacing={2}>
                  <TextField
                    label="Fabricante"
                    value={fabricante}
                    onChange={(e) => setFabricante(e.target.value)}
                    fullWidth
                    slotProps={{ htmlInput: { maxLength: 100 } }}
                  />
                  <TextField
                    label="Modelo"
                    value={modelo}
                    onChange={(e) => setModelo(e.target.value)}
                    fullWidth
                    slotProps={{ htmlInput: { maxLength: 100 } }}
                  />
                </Stack>
                <FormControlLabel
                  control={<Switch checked={colorida} onChange={(e) => setColorida(e.target.checked)} />}
                  label={colorida ? 'Colorida' : 'Monocromática'}
                />
                <Stack spacing={0.5}>
                  <FormControlLabel
                    control={<Switch checked={alugada} onChange={(e) => setAlugada(e.target.checked)} />}
                    label={alugada ? 'Alugada' : 'Própria'}
                  />
                  <Typography variant="caption" color="text.secondary">
                    Nas impressoras o aluguel é este interruptor, não o prefixo "MAQ" da identificação.
                  </Typography>
                </Stack>

                <ToggleButtonGroup
                  exclusive
                  fullWidth
                  size="small"
                  value={conexao}
                  onChange={(_, v) => v && trocarConexao(v)}
                >
                  <ToggleButton value="Rede">Em rede</ToggleButton>
                  <ToggleButton value="Usb">USB (ligada a um computador)</ToggleButton>
                </ToggleButtonGroup>
                {conexao === 'Rede' && (
                  <TextField
                    label="Endereço de rede (IP ou nome)"
                    value={enderecoRede}
                    onChange={(e) => setEnderecoRede(e.target.value)}
                    fullWidth
                    placeholder="192.168.0.50"
                    required={monitorarSnmp}
                    error={enderecoInvalido}
                    helperText={enderecoInvalido ? 'Endereço inválido. Use um IP ou um nome de rede.' : undefined}
                    slotProps={{ htmlInput: { maxLength: 255 } }}
                  />
                )}
                <TextField
                  label="MAC (opcional)"
                  value={macAddress}
                  onChange={(e) => setMacAddress(e.target.value)}
                  fullWidth
                  placeholder="AA:BB:CC:DD:EE:FF"
                  slotProps={{ htmlInput: { maxLength: 17 } }}
                />
                <Stack spacing={0.5}>
                  <FormControlLabel
                    control={
                      <Switch
                        checked={monitorarSnmp}
                        onChange={(e) => setMonitorarSnmp(e.target.checked)}
                        disabled={conexao === 'Usb'}
                      />
                    }
                    label="Monitorar por SNMP"
                  />
                  <Typography variant="caption" color="text.secondary">
                    {conexao === 'Usb'
                      ? 'Impressora USB não é alcançável pelo servidor: não há monitoramento.'
                      : 'O servidor consulta status, contador de páginas e suprimentos pelo endereço acima.'}
                  </Typography>
                </Stack>
              </>
            )}

            <TextField
              select
              label="Unidade que o dispositivo atende"
              value={unidadeId}
              onChange={(e) => setUnidadeId(e.target.value)}
              // Obrigatória quando o ativo é do local: é o nome dela que aparece no lugar do usuário.
              required={doLocal}
              fullWidth
            >
              <MenuItem value="">— Sem unidade —</MenuItem>
              {unidades?.map((u) => (
                <MenuItem key={u.id} value={u.id}>
                  {u.codigo ? `${u.nome} (${u.codigo})` : u.nome}
                </MenuItem>
              ))}
            </TextField>

            {ehImpressora ? (
              <Typography variant="caption" color="text.secondary">
                Impressora pertence ao local (unidade): não é atribuída a usuários nem tem termo de uso.
              </Typography>
            ) : (
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
          <Button
            type="submit"
            variant="contained"
            disabled={mutacaoAtiva.isPending || enderecoInvalido || (ehImpressora && !unidadeId)}
          >
            {mutacaoAtiva.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
