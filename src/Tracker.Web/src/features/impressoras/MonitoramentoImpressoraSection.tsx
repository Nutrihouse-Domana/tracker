import { useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  LinearProgress,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import RefreshIcon from '@mui/icons-material/RefreshOutlined'
import { useLeiturasImpressora, useLerImpressoraAgora, useMonitoramentoImpressora } from './hooks'
import type { SuprimentoImpressora } from './api'
import { PERMISSOES, usePermissao } from '../auth/permissoes'
import { formatarDataHora } from '../../utils/formato'
import { mensagemErroApi } from '../../utils/erroApi'

interface Props {
  dispositivoId: string
  monitorarSnmp: boolean
}

/** "aaaa-mm-dd" a partir das partes LOCAIS da data (nada de toISOString, que é UTC). */
function paraCampoData(data: Date): string {
  const mm = String(data.getMonth() + 1).padStart(2, '0')
  const dd = String(data.getDate()).padStart(2, '0')
  return `${data.getFullYear()}-${mm}-${dd}`
}

/** Mês corrente: do dia 1º até hoje. */
function periodoPadrao() {
  const hoje = new Date()
  return {
    de: paraCampoData(new Date(hoje.getFullYear(), hoje.getMonth(), 1)),
    ate: paraCampoData(hoje),
  }
}

/** "aaaa-mm-dd" → "dd/mm/aaaa" sem `new Date(texto)` (que leria como UTC e voltaria um dia). */
function formatarDiaCivil(valor: string): string {
  const [ano, mes, dia] = valor.slice(0, 10).split('-')
  return ano && mes && dia ? `${dia}/${mes}/${ano}` : valor
}

/** "há 5 min", "há 3 h", "há 2 dias". */
function tempoDecorrido(iso: string | null): string | null {
  if (!iso) return null
  const instante = new Date(iso).getTime()
  if (Number.isNaN(instante)) return null
  const minutos = Math.max(0, Math.round((Date.now() - instante) / 60_000))
  if (minutos < 1) return 'agora há pouco'
  if (minutos < 60) return `há ${minutos} min`
  const horas = Math.round(minutos / 60)
  if (horas < 48) return `há ${horas} h`
  return `há ${Math.round(horas / 24)} dias`
}

const numero = new Intl.NumberFormat('pt-BR')

function BarraSuprimento({ suprimento, limite }: { suprimento: SuprimentoImpressora; limite: number }) {
  const { percentual } = suprimento
  const baixo = percentual !== null && percentual <= limite
  const nome = suprimento.cor ? `${suprimento.descricao} (${suprimento.cor})` : suprimento.descricao

  return (
    <Box>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'baseline' }}>
        <Typography variant="body2">{nome}</Typography>
        <Typography variant="body2" color={baixo ? 'error' : 'text.secondary'} sx={{ fontWeight: baixo ? 600 : 400 }}>
          {percentual === null ? 'nível não informado' : `${percentual}%`}
        </Typography>
      </Stack>
      {percentual !== null && (
        <LinearProgress
          variant="determinate"
          value={percentual}
          color={baixo ? 'error' : 'success'}
          sx={{ height: 8, borderRadius: 4, mt: 0.5 }}
        />
      )}
    </Box>
  )
}

/**
 * Monitoramento SNMP de uma impressora: online/offline, estado, suprimentos, contador e as páginas
 * impressas num período (conferência da franquia da locação).
 */
export function MonitoramentoImpressoraSection({ dispositivoId, monitorarSnmp }: Props) {
  const podeLer = usePermissao(PERMISSOES.dispositivosCadastrar)
  const { data: estado, isLoading, error } = useMonitoramentoImpressora(dispositivoId, monitorarSnmp)
  const lerAgora = useLerImpressoraAgora(dispositivoId)

  const [periodo, setPeriodo] = useState(periodoPadrao)
  const periodoValido = !!periodo.de && !!periodo.ate && periodo.de <= periodo.ate
  const leituras = useLeiturasImpressora(dispositivoId, periodo.de, periodo.ate, monitorarSnmp && periodoValido)

  if (!monitorarSnmp) {
    return (
      <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
        Monitoramento SNMP desligado para esta impressora (ligada por USB ou não habilitada no cadastro).
      </Typography>
    )
  }

  if (isLoading) return <CircularProgress size={24} sx={{ mt: 1 }} />
  if (error || !estado)
    return (
      <Alert severity="error" sx={{ mt: 1 }}>
        {mensagemErroApi(error, 'Não foi possível carregar o monitoramento da impressora.')}
      </Alert>
    )

  const ultimaResposta = tempoDecorrido(estado.ultimaRespostaEm)
  const chip =
    estado.online === null
      ? { label: 'Ainda não lida', color: 'default' as const }
      : estado.online
        ? { label: 'Online', color: 'success' as const }
        : { label: 'Offline', color: 'error' as const }

  return (
    <Stack spacing={2} sx={{ mt: 1 }}>
      {!estado.monitoravel && (
        <Alert severity="warning">
          {estado.conexao === 'Usb'
            ? 'Impressora ligada por USB: o servidor não a alcança por SNMP.'
            : 'Informe o endereço de rede (IP ou nome) no cadastro para o servidor consultar esta impressora.'}
        </Alert>
      )}

      <Box>
        <Stack direction="row" spacing={2} sx={{ alignItems: 'center', flexWrap: 'wrap', rowGap: 1 }}>
          <Chip label={chip.label} color={chip.color} />
          <Typography variant="body2" color="text.secondary">
            Última resposta: {ultimaResposta ?? 'nunca'}
            {estado.ultimaRespostaEm && ` (${formatarDataHora(estado.ultimaRespostaEm)})`}
          </Typography>
          {podeLer && estado.monitoravel && (
            <Button
              size="small"
              variant="outlined"
              startIcon={<RefreshIcon />}
              disabled={lerAgora.isPending}
              onClick={() => lerAgora.mutate()}
            >
              {lerAgora.isPending ? 'Lendo...' : 'Ler agora'}
            </Button>
          )}
        </Stack>
        {estado.enderecoRede && (
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
            Endereço: {estado.enderecoRede}
            {estado.ultimaLeituraEm && ` · última tentativa ${formatarDataHora(estado.ultimaLeituraEm)}`}
          </Typography>
        )}
        {lerAgora.error && (
          <Alert severity="error" sx={{ mt: 1 }}>
            {mensagemErroApi(lerAgora.error, 'Não foi possível ler a impressora agora.')}
          </Alert>
        )}
        {estado.online === false && estado.erroUltimaLeitura && (
          <Alert severity="warning" sx={{ mt: 1 }}>
            {estado.erroUltimaLeitura}
          </Alert>
        )}
      </Box>

      <Stack direction="row" spacing={4} sx={{ flexWrap: 'wrap', rowGap: 1 }}>
        <Box>
          <Typography variant="caption" color="text.secondary">
            Estado reportado
          </Typography>
          <Typography variant="body2">{estado.estadoReportado ?? '—'}</Typography>
        </Box>
        <Box>
          <Typography variant="caption" color="text.secondary">
            Contador de páginas
          </Typography>
          <Typography variant="body2">
            {estado.contadorPaginas !== null ? numero.format(estado.contadorPaginas) : '—'}
          </Typography>
        </Box>
      </Stack>

      <Divider />

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Suprimentos
        </Typography>
        {estado.suprimentos.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            Nenhum suprimento informado pela impressora.
          </Typography>
        ) : (
          <Stack spacing={1.5}>
            {estado.suprimentos.map((s, i) => (
              <BarraSuprimento key={`${s.descricao}-${i}`} suprimento={s} limite={estado.limiteSuprimentoPercentual} />
            ))}
          </Stack>
        )}
      </Box>

      <Divider />

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Páginas no período
        </Typography>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ alignItems: { sm: 'center' } }}>
          <TextField
            label="De"
            type="date"
            size="small"
            value={periodo.de}
            onChange={(e) => setPeriodo((p) => ({ ...p, de: e.target.value }))}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            label="Até"
            type="date"
            size="small"
            value={periodo.ate}
            onChange={(e) => setPeriodo((p) => ({ ...p, ate: e.target.value }))}
            slotProps={{ inputLabel: { shrink: true } }}
          />
        </Stack>

        <Box sx={{ mt: 1.5 }}>
          {!periodoValido ? (
            <Typography variant="body2" color="error">
              Informe as duas datas, com a inicial antes da final.
            </Typography>
          ) : leituras.isLoading ? (
            <CircularProgress size={20} />
          ) : leituras.error ? (
            <Alert severity="error">{mensagemErroApi(leituras.error, 'Não foi possível carregar as leituras.')}</Alert>
          ) : leituras.data ? (
            leituras.data.paginasNoPeriodo !== null ? (
              <>
                <Typography variant="h6">{numero.format(leituras.data.paginasNoPeriodo)} páginas</Typography>
                <Typography variant="body2" color="text.secondary">
                  De {formatarDiaCivil(leituras.data.de)} a {formatarDiaCivil(leituras.data.ate)}: contador de{' '}
                  {numero.format(leituras.data.contadorInicial ?? 0)} a {numero.format(leituras.data.contadorFinal ?? 0)}{' '}
                  ({leituras.data.totalLeituras} leituras).
                </Typography>
              </>
            ) : (
              <Typography variant="body2" color="text.secondary">
                Leituras insuficientes no período para calcular (são precisas ao menos duas com contador).
              </Typography>
            )
          ) : null}
        </Box>
      </Box>
    </Stack>
  )
}
