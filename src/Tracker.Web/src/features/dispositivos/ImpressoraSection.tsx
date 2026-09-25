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
  Grid,
  MenuItem,
  Stack,
  Switch,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material'
import EditOutlinedIcon from '@mui/icons-material/EditOutlined'
import type { ConexaoImpressora, Dispositivo, TipoImpressora } from '../../api/types'
import {
  useDefinirAluguelImpressora,
  useDefinirConexaoImpressora,
  useDefinirDadosImpressora,
} from './hooks'
import { mensagemErroApi } from '../../utils/erroApi'
import { usePermissoes, PERMISSOES } from '../auth/permissoes'
import {
  enderecoRedePareceValido,
  rotuloConexao,
  rotuloTipoImpressora,
  tiposImpressora,
} from './impressora'

interface Props {
  dispositivo: Dispositivo
}

function Campo({ rotulo, valor }: { rotulo: string; valor: React.ReactNode }) {
  return (
    <Grid size={{ xs: 6, sm: 4 }}>
      <Typography variant="caption" color="text.secondary">
        {rotulo}
      </Typography>
      <Typography variant="body2">{valor ?? '—'}</Typography>
    </Grid>
  )
}

/**
 * "Dados da impressora": tipo/fabricante/modelo, o interruptor de aluguel (nas impressoras o
 * aluguel NÃO segue o prefixo "MAQ") e a conexão (rede/USB, endereço, SNMP). Nada aqui pede a
 * senha do ADM — são dados do cadastro/contrato —, mas tudo é auditado no backend.
 */
export function ImpressoraSection({ dispositivo }: Props) {
  const { tem } = usePermissoes()
  const podeCadastrar = tem(PERMISSOES.dispositivosCadastrar)
  const definirAluguel = useDefinirAluguelImpressora()
  const [dadosAberto, setDadosAberto] = useState(false)
  const [conexaoAberta, setConexaoAberta] = useState(false)

  const alugada = dispositivo.alugada === true

  return (
    <Stack spacing={3}>
      <Stack spacing={0.5}>
        {podeCadastrar ? (
          <FormControlLabel
            control={
              <Switch
                checked={alugada}
                disabled={definirAluguel.isPending}
                onChange={(e) =>
                  definirAluguel.mutate({ id: dispositivo.id, data: { alugada: e.target.checked } })
                }
              />
            }
            label={alugada ? 'Alugada' : 'Própria'}
          />
        ) : (
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {alugada ? 'Alugada' : 'Própria'}
          </Typography>
        )}
        <Typography variant="caption" color="text.secondary">
          Nas impressoras o aluguel é este interruptor, não o prefixo "MAQ" da identificação. O
          vencimento do contrato fica no cabeçalho, como nos demais ativos.
        </Typography>
        {definirAluguel.isError && (
          <Alert severity="error">
            {mensagemErroApi(definirAluguel.error, 'Não foi possível alterar o aluguel.')}
          </Alert>
        )}
      </Stack>

      <Stack spacing={1}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography variant="subtitle2">Equipamento</Typography>
          {podeCadastrar && (
            <Button size="small" startIcon={<EditOutlinedIcon />} onClick={() => setDadosAberto(true)}>
              Editar
            </Button>
          )}
        </Stack>
        <Grid container spacing={2}>
          <Campo
            rotulo="Tipo"
            valor={dispositivo.tipoImpressora ? rotuloTipoImpressora[dispositivo.tipoImpressora] : null}
          />
          <Campo rotulo="Fabricante" valor={dispositivo.fabricante} />
          <Campo rotulo="Modelo" valor={dispositivo.modelo} />
          <Campo rotulo="Impressão" valor={dispositivo.colorida ? 'Colorida' : 'Monocromática'} />
        </Grid>
      </Stack>

      <Stack spacing={1}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography variant="subtitle2">Conexão</Typography>
          {podeCadastrar && (
            <Button size="small" startIcon={<EditOutlinedIcon />} onClick={() => setConexaoAberta(true)}>
              Editar
            </Button>
          )}
        </Stack>
        <Grid container spacing={2}>
          <Campo rotulo="Ligação" valor={dispositivo.conexao ? rotuloConexao[dispositivo.conexao] : null} />
          {dispositivo.conexao !== 'Usb' && <Campo rotulo="Endereço de rede" valor={dispositivo.enderecoRede} />}
          <Campo rotulo="MAC" valor={dispositivo.macAddress} />
          <Campo rotulo="Monitoramento SNMP" valor={dispositivo.monitorarSnmp ? 'Ligado' : 'Desligado'} />
        </Grid>
      </Stack>

      <DadosImpressoraDialog
        dispositivo={dadosAberto ? dispositivo : null}
        onClose={() => setDadosAberto(false)}
      />
      <ConexaoImpressoraDialog
        dispositivo={conexaoAberta ? dispositivo : null}
        onClose={() => setConexaoAberta(false)}
      />
    </Stack>
  )
}

interface DialogProps {
  dispositivo: Dispositivo | null
  onClose: () => void
}

function DadosImpressoraDialog({ dispositivo, onClose }: DialogProps) {
  const [tipoImpressora, setTipoImpressora] = useState<TipoImpressora>('Laser')
  const [fabricante, setFabricante] = useState('')
  const [modelo, setModelo] = useState('')
  const [colorida, setColorida] = useState(false)
  const definir = useDefinirDadosImpressora()

  const dispositivoId = dispositivo?.id
  // Reseta só ao abrir/trocar de aparelho: o objeto `dispositivo` muda a cada refetch e apagaria
  // o que está sendo digitado (ver CLAUDE.md).
  useEffect(() => {
    if (!dispositivo) return
    setTipoImpressora(dispositivo.tipoImpressora ?? 'Laser')
    setFabricante(dispositivo.fabricante ?? '')
    setModelo(dispositivo.modelo ?? '')
    setColorida(dispositivo.colorida === true)
    definir.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dispositivoId])

  if (!dispositivo) return null

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    definir.mutate(
      {
        id: dispositivo.id,
        data: {
          tipoImpressora,
          fabricante: fabricante.trim() || null,
          modelo: modelo.trim() || null,
          colorida,
        },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Dados da impressora</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {definir.isError && (
              <Alert severity="error">
                {mensagemErroApi(definir.error, 'Não foi possível salvar os dados da impressora.')}
              </Alert>
            )}
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
            <FormControlLabel
              control={<Switch checked={colorida} onChange={(e) => setColorida(e.target.checked)} />}
              label={colorida ? 'Colorida' : 'Monocromática'}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={definir.isPending}>
            {definir.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}

function ConexaoImpressoraDialog({ dispositivo, onClose }: DialogProps) {
  const [conexao, setConexao] = useState<ConexaoImpressora>('Rede')
  const [enderecoRede, setEnderecoRede] = useState('')
  const [macAddress, setMacAddress] = useState('')
  const [monitorarSnmp, setMonitorarSnmp] = useState(false)
  const definir = useDefinirConexaoImpressora()

  const dispositivoId = dispositivo?.id
  useEffect(() => {
    if (!dispositivo) return
    setConexao(dispositivo.conexao ?? 'Rede')
    setEnderecoRede(dispositivo.enderecoRede ?? '')
    setMacAddress(dispositivo.macAddress ?? '')
    setMonitorarSnmp(dispositivo.monitorarSnmp === true)
    definir.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dispositivoId])

  if (!dispositivo) return null

  const enderecoInvalido = conexao === 'Rede' && enderecoRede.trim() !== '' && !enderecoRedePareceValido(enderecoRede)
  const faltaEndereco = conexao === 'Rede' && monitorarSnmp && enderecoRede.trim() === ''

  const trocarConexao = (nova: ConexaoImpressora) => {
    setConexao(nova)
    // USB não é alcançável pelo servidor: o backend também força SNMP desligado e endereço nulo.
    if (nova === 'Usb') setMonitorarSnmp(false)
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    definir.mutate(
      {
        id: dispositivo.id,
        data: {
          conexao,
          enderecoRede: conexao === 'Rede' ? enderecoRede.trim() || null : null,
          macAddress: macAddress.trim() || null,
          monitorarSnmp: conexao === 'Rede' && monitorarSnmp,
        },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Conexão da impressora</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <DialogContentText>
              Só impressoras em rede podem ser monitoradas por SNMP. As ligadas por USB a um
              computador ficam apenas no inventário.
            </DialogContentText>
            {definir.isError && (
              <Alert severity="error">
                {mensagemErroApi(definir.error, 'Não foi possível salvar a conexão.')}
              </Alert>
            )}
            <ToggleButtonGroup
              exclusive
              fullWidth
              size="small"
              value={conexao}
              onChange={(_, v) => v && trocarConexao(v)}
            >
              <ToggleButton value="Rede">Em rede</ToggleButton>
              <ToggleButton value="Usb">USB</ToggleButton>
            </ToggleButtonGroup>
            {conexao === 'Rede' && (
              <TextField
                label="Endereço de rede (IP ou nome)"
                value={enderecoRede}
                onChange={(e) => setEnderecoRede(e.target.value)}
                fullWidth
                autoFocus
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
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>Cancelar</Button>
          <Button
            type="submit"
            variant="contained"
            disabled={definir.isPending || enderecoInvalido || faltaEndereco}
          >
            {definir.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
