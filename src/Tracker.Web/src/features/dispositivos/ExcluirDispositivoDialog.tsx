import { useEffect, useState } from 'react'
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogContentText,
  DialogActions,
  TextField,
  Button,
  Alert,
  AlertTitle,
  Stack,
  Radio,
  RadioGroup,
  FormControlLabel,
  Typography,
} from '@mui/material'
import type { Dispositivo } from '../../api/types'
import { useExcluirDispositivo } from './hooks'
import { useTermosPorDispositivo } from '../termos/hooks'
import { mensagemErroApi } from '../../utils/erroApi'
import { PERMISSOES, usePermissao } from '../auth/permissoes'
import { identificacaoExibida } from './aluguel'

interface Props {
  dispositivo: Dispositivo | null
  onClose: () => void
  /** Chamado após a exclusão dar certo — a tela de detalhe usa para voltar à lista. */
  onExcluido?: () => void
}

type Modo = 'arquivar' | 'definitiva'

/**
 * Confirmação de saída de um dispositivo do inventário. Pergunta antes se é arquivamento
 * (restaurável) ou exclusão definitiva, e pede a senha do ADM logado nos dois casos: o cookie
 * de sessão sozinho não é prova de quem está na frente da máquina.
 */
export function ExcluirDispositivoDialog({ dispositivo, onClose, onExcluido }: Props) {
  const [modo, setModo] = useState<Modo>('arquivar')
  const [senha, setSenha] = useState('')
  const [motivo, setMotivo] = useState('')
  const [confirmacao, setConfirmacao] = useState('')
  const excluir = useExcluirDispositivo()
  // "Ocultar, não desabilitar": sem a permissão, a opção definitiva nem aparece (o backend recusa).
  const podeExcluirDefinitivo = usePermissao(PERMISSOES.dispositivosExcluirDefinitivo)

  // Quantos termos assinados serão destruídos junto: é a informação que muda a decisão.
  const { data: termos } = useTermosPorDispositivo(dispositivo?.id)

  // Sem a permissão, nunca é definitiva (mesmo que o estado diga o contrário, ex.: já arquivado).
  const definitiva = modo === 'definitiva' && podeExcluirDefinitivo
  const identificacao = dispositivo ? identificacaoExibida(dispositivo) : ''
  // Já arquivado só tem um caminho adiante: arquivar de novo não faria nada.
  const jaArquivado = !!dispositivo?.excluidoEm
  // Digitar EXCLUIR evita que a opção destrutiva seja disparada por um clique distraído.
  const confirmacaoOk = !definitiva || confirmacao.trim().toUpperCase() === 'EXCLUIR'
  // Arquivado sem a permissão definitiva: não sobra ação nenhuma neste diálogo.
  const semAcao = jaArquivado && !podeExcluirDefinitivo

  const dispositivoId = dispositivo?.id
  // Reseta só quando o diálogo ABRE ou troca de aparelho (muda o id). O objeto `dispositivo` é
  // recriado a cada refetch da query e, com ele na dependência, a senha/o texto digitado sumia.
  useEffect(() => {
    if (!dispositivo) return
    // Arquivar é o padrão: a opção sem volta precisa ser uma escolha consciente.
    setModo(dispositivo.excluidoEm ? 'definitiva' : 'arquivar')
    setSenha('')
    setMotivo('')
    setConfirmacao('')
    excluir.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dispositivoId])

  const handleClose = () => {
    excluir.reset()
    onClose()
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!dispositivo || !confirmacaoOk || semAcao) return
    excluir.mutate(
      { id: dispositivo.id, data: { senha, motivo: motivo.trim() || null, definitiva } },
      {
        onSuccess: () => {
          handleClose()
          onExcluido?.()
        },
      },
    )
  }

  return (
    <Dialog open={!!dispositivo} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Excluir dispositivo</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2}>
            <DialogContentText>
              {semAcao ? (
                <>
                  <strong>{identificacao}</strong> já está arquivado. Apagá-lo de vez exige a
                  permissão "Excluir definitivo", que você não tem.
                </>
              ) : jaArquivado ? (
                <>
                  <strong>{identificacao}</strong> já está arquivado. A única ação restante é
                  apagá-lo de vez.
                </>
              ) : !podeExcluirDefinitivo ? (
                <>
                  Arquivar <strong>{identificacao}</strong>? Sai do inventário, da busca e do
                  dashboard; histórico e termos são preservados e ele pode ser restaurado na aba
                  Arquivados.
                </>
              ) : (
                <>
                  O que fazer com <strong>{identificacao}</strong>?
                </>
              )}
            </DialogContentText>

            <RadioGroup
              value={modo}
              onChange={(e) => setModo(e.target.value as Modo)}
              sx={{ display: jaArquivado || !podeExcluirDefinitivo ? 'none' : undefined }}
            >
              <FormControlLabel
                value="arquivar"
                control={<Radio />}
                label={
                  <Stack>
                    <Typography variant="body2">Arquivar</Typography>
                    <Typography variant="caption" color="text.secondary">
                      Sai do inventário, da busca e do dashboard. Histórico e termos preservados;
                      pode ser restaurado na aba Arquivados.
                    </Typography>
                  </Stack>
                }
              />
              {podeExcluirDefinitivo && (
                <FormControlLabel
                  value="definitiva"
                  control={<Radio color="error" />}
                  label={
                    <Stack>
                      <Typography variant="body2">Excluir definitivamente</Typography>
                      <Typography variant="caption" color="text.secondary">
                        Apaga o registro e tudo que está ligado a ele. Não tem volta.
                      </Typography>
                    </Stack>
                  }
                />
              )}
            </RadioGroup>

            {definitiva && (
              <Alert severity="error">
                <AlertTitle>Isso não pode ser desfeito</AlertTitle>
                Serão apagados com o dispositivo: movimentações, reparos, comentários, alertas,
                histórico de localização e comandos remotos
                {termos?.length
                  ? ` — além de ${termos.length} termo(s) de uso assinado(s) e o(s) PDF(s) em disco`
                  : ''}
                .
              </Alert>
            )}

            {excluir.isError && (
              <Alert severity="error">
                {mensagemErroApi(excluir.error, 'Não foi possível excluir o dispositivo.')}
              </Alert>
            )}

            {!semAcao && (
              <>
                <TextField
                  label="Motivo (opcional)"
                  value={motivo}
                  onChange={(e) => setMotivo(e.target.value)}
                  fullWidth
                  multiline
                  minRows={2}
                  placeholder="Descarte, roubo, devolução ao fornecedor..."
                  slotProps={{ htmlInput: { maxLength: 400 } }}
                />

                {definitiva && (
                  <TextField
                    label="Digite EXCLUIR para confirmar"
                    value={confirmacao}
                    onChange={(e) => setConfirmacao(e.target.value)}
                    required
                    fullWidth
                  />
                )}

                <TextField
                  label="Sua senha do Tracker"
                  type="password"
                  value={senha}
                  onChange={(e) => setSenha(e.target.value)}
                  required
                  fullWidth
                  autoComplete="current-password"
                  helperText="Confirme com a mesma senha que você usa para entrar."
                />
              </>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>{semAcao ? 'Fechar' : 'Cancelar'}</Button>
          {!semAcao && (
            <Button
              type="submit"
              variant="contained"
              color="error"
              disabled={excluir.isPending || !senha || !confirmacaoOk}
            >
              {excluir.isPending
                ? 'Excluindo...'
                : definitiva
                  ? 'Excluir definitivamente'
                  : 'Arquivar'}
            </Button>
          )}
        </DialogActions>
      </form>
    </Dialog>
  )
}
