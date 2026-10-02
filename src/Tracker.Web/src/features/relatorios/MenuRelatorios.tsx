import { useState } from 'react'
import { Alert, Menu, MenuItem, Stack, Typography } from '@mui/material'
import ChevronRightIcon from '@mui/icons-material/ChevronRightOutlined'
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdfOutlined'
import TableChartIcon from '@mui/icons-material/TableChartOutlined'
import { useBaixarRelatorio } from './hooks'
import type { FormatoRelatorio, TipoRelatorio } from './api'
import { mensagemErroApi } from '../../utils/erroApi'

const relatorios: { tipo: TipoRelatorio; titulo: string }[] = [
  { tipo: 'inventario', titulo: 'Inventário de ativos' },
  { tipo: 'aluguel', titulo: 'Alugados x próprios' },
  { tipo: 'unidades', titulo: 'Parque por unidade' },
  { tipo: 'termos', titulo: 'Termo de uso dos computadores' },
]

const formatos: { formato: FormatoRelatorio; rotulo: string }[] = [
  { formato: 'pdf', rotulo: 'PDF' },
  { formato: 'xlsx', rotulo: 'Excel' },
]

/**
 * Item "Relatórios" do menu da conta (o que abre ao clicar no próprio nome). Abre em dois níveis:
 * primeiro só a lista de relatórios; ao clicar num deles, um terceiro menu oferece PDF ou Excel e
 * baixa na hora.
 */
export function MenuRelatorios({ onBaixou }: { onBaixou: () => void }) {
  const [ancora, setAncora] = useState<null | HTMLElement>(null)
  const [ancoraFormato, setAncoraFormato] = useState<null | HTMLElement>(null)
  const [escolhido, setEscolhido] = useState<TipoRelatorio | null>(null)
  const baixar = useBaixarRelatorio()
  const [baixando, setBaixando] = useState<FormatoRelatorio | null>(null)

  /** Fecha só o menu de formatos e limpa o aviso de erro dele. */
  const fecharFormatos = () => {
    setAncoraFormato(null)
    setEscolhido(null)
    baixar.reset()
  }

  const gerar = (formato: FormatoRelatorio) => {
    if (!escolhido) return
    setBaixando(formato)
    baixar.mutate(
      { tipo: escolhido, formato },
      {
        onSuccess: () => {
          setAncoraFormato(null)
          setEscolhido(null)
          setAncora(null)
          onBaixou()   // fecha também o menu da conta, que é o pai deste
        },
        // Na falha os menus ficam ABERTOS com o aviso: fechar desmontaria este componente (o menu
        // da conta não mantém os filhos montados) e o erro sumiria junto.
        onSettled: () => setBaixando(null),
      },
    )
  }

  // Painel elevado no escuro ganha dois tons por causa do degradê de elevação do MUI: sem
  // backgroundImage e com borda, fica de um tom só (ver CLAUDE.md).
  const papel = { backgroundImage: 'none', border: 1, borderColor: 'divider' }

  return (
    <>
      <MenuItem onClick={(e) => setAncora(e.currentTarget)}>
        <Stack direction="row" spacing={2} sx={{ alignItems: 'center', justifyContent: 'space-between', width: '100%' }}>
          <span>Relatórios</span>
          <ChevronRightIcon fontSize="small" sx={{ color: 'text.secondary' }} />
        </Stack>
      </MenuItem>

      <Menu
        anchorEl={ancora}
        open={Boolean(ancora)}
        onClose={() => {
          fecharFormatos()
          setAncora(null)
        }}
        anchorOrigin={{ vertical: 'top', horizontal: 'left' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { ...papel, minWidth: 260 } } }}
      >
        {relatorios.map((r) => (
          <MenuItem
            key={r.tipo}
            selected={escolhido === r.tipo}
            onClick={(e) => {
              baixar.reset()
              setEscolhido(r.tipo)
              setAncoraFormato(e.currentTarget)
            }}
          >
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center', justifyContent: 'space-between', width: '100%' }}>
              <Typography variant="body2">{r.titulo}</Typography>
              <ChevronRightIcon fontSize="small" sx={{ color: 'text.secondary' }} />
            </Stack>
          </MenuItem>
        ))}
      </Menu>

      <Menu
        anchorEl={ancoraFormato}
        open={Boolean(ancoraFormato)}
        onClose={fecharFormatos}
        anchorOrigin={{ vertical: 'top', horizontal: 'left' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { ...papel, minWidth: 160 } } }}
      >
        {baixar.isError && (
          <Alert severity="error" sx={{ mx: 1, mb: 1, maxWidth: 320 }} onClose={() => baixar.reset()}>
            {mensagemErroApi(baixar.error, 'Não foi possível gerar o relatório.')}
          </Alert>
        )}
        {formatos.map((f) => (
          <MenuItem key={f.formato} disabled={baixando !== null} onClick={() => gerar(f.formato)}>
            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
              {f.formato === 'pdf' ? (
                <PictureAsPdfIcon fontSize="small" sx={{ color: 'text.secondary' }} />
              ) : (
                <TableChartIcon fontSize="small" sx={{ color: 'text.secondary' }} />
              )}
              <Typography variant="body2">{baixando === f.formato ? 'Gerando…' : f.rotulo}</Typography>
            </Stack>
          </MenuItem>
        ))}
      </Menu>
    </>
  )
}
