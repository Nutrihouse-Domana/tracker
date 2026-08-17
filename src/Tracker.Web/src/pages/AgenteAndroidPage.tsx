import { useState } from 'react'
import {
  Typography,
  Button,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Paper,
  TableContainer,
  Chip,
  Stack,
  CircularProgress,
  Box,
  Card,
  CardContent,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Alert,
} from '@mui/material'
import UploadIcon from '@mui/icons-material/UploadFileOutlined'
import AndroidIcon from '@mui/icons-material/AndroidOutlined'
import { useSituacaoAgenteAndroid, usePublicarPacote } from '../features/agenteAndroid/hooks'
import { mensagemErroApi } from '../utils/erroApi'

function formatarTamanho(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  return `${Math.ceil(bytes / 1024)} KB`
}

function formatarData(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })
}

/**
 * Publicação do APK do agente Android para atualização OTA: a frota baixa e instala sozinha
 * (oferta no heartbeat), sem loja de aplicativos e sem tocar nos aparelhos.
 */
export function AgenteAndroidPage() {
  const { data: situacao, isLoading } = useSituacaoAgenteAndroid()
  const [dialogAberto, setDialogAberto] = useState(false)

  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: 'space-between', alignItems: 'center' }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <AndroidIcon />
          <Typography variant="h5">Agente Android</Typography>
        </Stack>
        <Button variant="contained" startIcon={<UploadIcon />} onClick={() => setDialogAberto(true)}>
          Publicar nova versão
        </Button>
      </Stack>

      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Ao publicar um APK aqui, os tablets e celulares se atualizam sozinhos no próximo
        contato com o servidor — sem loja de aplicativos. Aparelhos com Device Owner instalam
        em silêncio; nos demais o Android pede uma confirmação na tela na primeira vez.
      </Typography>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <Stack spacing={3}>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography variant="overline" color="text.secondary">
                  Versão vigente
                </Typography>
                <Typography variant="h5">
                  {situacao?.pacoteVigente
                    ? `${situacao.pacoteVigente.versaoNome} (code ${situacao.pacoteVigente.versaoCode})`
                    : 'Nenhuma publicada'}
                </Typography>
                {situacao?.pacoteVigente && (
                  <Typography variant="caption" color="text.secondary">
                    Publicada em {formatarData(situacao.pacoteVigente.criadoEm)} ·{' '}
                    {formatarTamanho(situacao.pacoteVigente.tamanhoBytes)}
                  </Typography>
                )}
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography variant="overline" color="text.secondary">
                  Aparelhos na versão
                </Typography>
                <Typography variant="h5">{situacao?.dispositivosNaVersao ?? 0}</Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography variant="overline" color="text.secondary">
                  Desatualizados
                </Typography>
                <Typography
                  variant="h5"
                  color={situacao?.dispositivosDesatualizados ? 'warning.main' : undefined}
                >
                  {situacao?.dispositivosDesatualizados ?? 0}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Atualizam sozinhos no próximo heartbeat.
                </Typography>
              </CardContent>
            </Card>
          </Stack>

          <TableContainer component={Paper}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Versão</TableCell>
                  <TableCell align="center">versionCode</TableCell>
                  <TableCell>Arquivo</TableCell>
                  <TableCell align="right">Tamanho</TableCell>
                  <TableCell>Publicado em</TableCell>
                  <TableCell align="center">Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {situacao?.historico.map((p, indice) => (
                  <TableRow key={p.id} hover>
                    <TableCell>{p.versaoNome}</TableCell>
                    <TableCell align="center">{p.versaoCode}</TableCell>
                    <TableCell>{p.nomeArquivoOriginal}</TableCell>
                    <TableCell align="right">{formatarTamanho(p.tamanhoBytes)}</TableCell>
                    <TableCell>{formatarData(p.criadoEm)}</TableCell>
                    <TableCell align="center">
                      {indice === 0 ? (
                        <Chip size="small" label="Vigente" color="success" />
                      ) : (
                        <Chip size="small" label="Substituída" />
                      )}
                    </TableCell>
                  </TableRow>
                ))}
                {situacao?.historico.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6} align="center">
                      Nenhum pacote publicado ainda. Gere o APK com{' '}
                      <code>./gradlew :app:assembleRelease</code> e publique aqui.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </Stack>
      )}

      <PublicarPacoteDialog
        open={dialogAberto}
        versaoCodeMinima={(situacao?.pacoteVigente?.versaoCode ?? 0) + 1}
        onClose={() => setDialogAberto(false)}
      />
    </>
  )
}

interface DialogProps {
  open: boolean
  versaoCodeMinima: number
  onClose: () => void
}

/**
 * Upload do APK. Pede a senha do ADM logado: publicar um pacote instala software na frota
 * inteira — mesma régua de segurança de excluir dispositivo.
 */
function PublicarPacoteDialog({ open, versaoCodeMinima, onClose }: DialogProps) {
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [versaoCode, setVersaoCode] = useState('')
  const [versaoNome, setVersaoNome] = useState('')
  const [senha, setSenha] = useState('')
  const publicar = usePublicarPacote()

  const handleClose = () => {
    setArquivo(null)
    setVersaoCode('')
    setVersaoNome('')
    setSenha('')
    publicar.reset()
    onClose()
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!arquivo) return
    publicar.mutate(
      { arquivo, versaoCode: Number(versaoCode), versaoNome: versaoNome.trim(), senha },
      { onSuccess: handleClose },
    )
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Publicar nova versão do agente</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Stack spacing={2}>
            <Alert severity="info">
              Informe o <strong>versionCode</strong> e o <strong>versionName</strong> exatamente
              como estão no <code>build.gradle.kts</code> do APK. O versionCode precisa ser maior
              que o vigente (mínimo: {versaoCodeMinima}) — o Android recusa downgrade.
            </Alert>

            {publicar.isError && (
              <Alert severity="error">
                {mensagemErroApi(publicar.error, 'Não foi possível publicar o pacote.')}
              </Alert>
            )}

            <Button variant="outlined" component="label" startIcon={<UploadIcon />}>
              {arquivo ? arquivo.name : 'Escolher arquivo .apk'}
              <input
                type="file"
                hidden
                accept=".apk"
                onChange={(e) => setArquivo(e.target.files?.[0] ?? null)}
              />
            </Button>

            <Stack direction="row" spacing={2}>
              <TextField
                label="versionCode"
                value={versaoCode}
                onChange={(e) => setVersaoCode(e.target.value.replace(/\D/g, ''))}
                required
                fullWidth
                slotProps={{ htmlInput: { inputMode: 'numeric' } }}
              />
              <TextField
                label="versionName"
                value={versaoNome}
                onChange={(e) => setVersaoNome(e.target.value)}
                required
                fullWidth
                placeholder="1.1.0"
              />
            </Stack>

            <TextField
              label="Sua senha do Tracker"
              type="password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              required
              fullWidth
              autoComplete="current-password"
              helperText="Publicar um APK atualiza todos os aparelhos da frota — confirme com sua senha."
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancelar</Button>
          <Button
            type="submit"
            variant="contained"
            disabled={publicar.isPending || !arquivo || !versaoCode || !versaoNome || !senha}
          >
            {publicar.isPending ? 'Publicando...' : 'Publicar'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
