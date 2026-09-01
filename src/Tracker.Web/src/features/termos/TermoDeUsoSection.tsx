import { useRef, useState } from 'react'
import { Button, Stack, Typography, List, ListItem, ListItemText, Alert, Divider, IconButton, TextField } from '@mui/material'
import VisibilityIcon from '@mui/icons-material/VisibilityOutlined'
import type { Dispositivo } from '../../api/types'
import { useGerarPdfTermo, useTermosPorDispositivo, useUploadTermo, useVisualizarTermo } from './hooks'
import { abrirPdfEmNovaAba, baixarPdfComoArquivo } from './api'
import { usePermissao, PERMISSOES } from '../auth/permissoes'
import { formatarDataHora } from '../../utils/formato'

interface Props {
  dispositivo: Dispositivo
}

export function TermoDeUsoSection({ dispositivo }: Props) {
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [estadoDispositivo, setEstadoDispositivo] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  const { data: termos } = useTermosPorDispositivo(dispositivo.id)
  const podeMovimentar = usePermissao(PERMISSOES.dispositivosMovimentar)
  const gerarPdf = useGerarPdfTermo()
  const upload = useUploadTermo()
  const visualizar = useVisualizarTermo()

  const usuarioId = dispositivo.usuarioAtualId
  // v2.0: texto legal ampliado (7 cláusulas, equipamento próprio/locado, art. 462 §1º da CLT).
  // Termos assinados sob o texto anterior permanecem gravados como v1.0.
  const versaoTermo = '2.0'

  const handleGerar = () => {
    if (!usuarioId) return
    gerarPdf.mutate(
      { dispositivoId: dispositivo.id, usuarioId, versaoTermo, estadoDispositivo: estadoDispositivo.trim() || undefined },
      { onSuccess: (blob) => baixarPdfComoArquivo(blob, `termo-uso-${dispositivo.numeroSerie}.pdf`) },
    )
  }

  const handleUpload = () => {
    if (!usuarioId || !arquivo) return
    upload.mutate(
      { dispositivoId: dispositivo.id, usuarioId, versaoTermo, arquivo },
      {
        onSuccess: () => {
          setArquivo(null)
          if (inputRef.current) inputRef.current.value = ''
        },
      },
    )
  }

  return (
    <Stack spacing={2} sx={{ mt: 1 }}>
      {podeMovimentar && !usuarioId && (
        <Alert severity="warning">
          Este dispositivo ainda não foi atribuído a um usuário — atribua antes de gerar o termo.
        </Alert>
      )}

      {podeMovimentar && (
        <>
          <Typography variant="subtitle2">1. Gerar termo para impressão</Typography>
          <TextField
            label="Estado do dispositivo"
            placeholder="Ex.: Novo, sem avarias; ou: usado, risco na tampa."
            value={estadoDispositivo}
            onChange={(e) => setEstadoDispositivo(e.target.value)}
            multiline
            minRows={2}
            fullWidth
            disabled={!usuarioId}
            slotProps={{ htmlInput: { maxLength: 500 } }}
          />
          <Button variant="outlined" disabled={!usuarioId || gerarPdf.isPending} onClick={handleGerar} sx={{ alignSelf: 'flex-start' }}>
            {gerarPdf.isPending ? 'Gerando...' : 'Gerar e baixar PDF'}
          </Button>

          <Divider />

          <Typography variant="subtitle2">2. Anexar termo assinado (escaneado)</Typography>
          <input
            ref={inputRef}
            type="file"
            accept="application/pdf"
            onChange={(e) => setArquivo(e.target.files?.[0] ?? null)}
          />
          {upload.isError && <Alert severity="error">Falha ao enviar o arquivo. Confirme que é um PDF.</Alert>}
          <Button
            variant="contained"
            disabled={!usuarioId || !arquivo || upload.isPending}
            onClick={handleUpload}
            sx={{ alignSelf: 'flex-start' }}
          >
            {upload.isPending ? 'Enviando...' : 'Enviar termo assinado'}
          </Button>

          <Divider />
        </>
      )}

      <Typography variant="subtitle2">Termos já anexados</Typography>
      <List dense>
        {termos?.map((t) => (
          <ListItem
            key={t.id}
            secondaryAction={
              <IconButton
                edge="end"
                title="Visualizar PDF"
                onClick={() => visualizar.mutate(t.id, { onSuccess: abrirPdfEmNovaAba })}
              >
                <VisibilityIcon fontSize="small" />
              </IconButton>
            }
          >
            <ListItemText
              primary={`${t.usuarioNome} — v${t.versaoTermo}`}
              secondary={formatarDataHora(t.dataAceite)}
            />
          </ListItem>
        ))}
        {termos?.length === 0 && (
          <ListItem>
            <ListItemText secondary="Nenhum termo anexado ainda." />
          </ListItem>
        )}
      </List>
    </Stack>
  )
}
