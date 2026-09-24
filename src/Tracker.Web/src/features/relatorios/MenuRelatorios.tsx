import { useState } from 'react'
import { Alert, ListSubheader, Menu, MenuItem, Stack, Typography } from '@mui/material'
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
]

const formatos: { formato: FormatoRelatorio; rotulo: string }[] = [
  { formato: 'pdf', rotulo: 'PDF' },
  { formato: 'xlsx', rotulo: 'Excel' },
]

/**
 * Item "Relatórios" do menu da conta (o que abre ao clicar no próprio nome), com um submenu que
 * baixa cada relatório na hora. É atalho: a tela de Perfil segue mostrando os mesmos relatórios
 * com os números do parque antes do download.
 */
export function MenuRelatorios({ onBaixou }: { onBaixou: () => void }) {
  const [ancora, setAncora] = useState<null | HTMLElement>(null)
  const baixar = useBaixarRelatorio()
  const [baixando, setBaixando] = useState<string | null>(null)

  const gerar = (tipo: TipoRelatorio, formato: FormatoRelatorio) => {
    const chave = `${tipo}-${formato}`
    setBaixando(chave)
    baixar.mutate(
      { tipo, formato },
      {
        onSuccess: () => {
          setAncora(null)
          onBaixou()   // fecha também o menu da conta, que é o pai deste
        },
        // Na falha os menus ficam ABERTOS com o aviso: fechar desmontaria este componente (o menu
        // da conta não mantém os filhos montados) e o erro sumiria junto, como acontecia antes.
        onSettled: () => setBaixando(null),
      },
    )
  }

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
          setAncora(null)
          baixar.reset()
        }}
        anchorOrigin={{ vertical: 'top', horizontal: 'left' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        // Painel com subheader no escuro ganha dois tons por causa do degradê de elevação do MUI:
        // sem backgroundImage e com borda, fica de um tom só (ver CLAUDE.md).
        slotProps={{ paper: { sx: { backgroundImage: 'none', border: 1, borderColor: 'divider', minWidth: 240 } } }}
      >
        {baixar.isError && (
          <Alert severity="error" sx={{ mx: 1, mb: 1, maxWidth: 320 }} onClose={() => baixar.reset()}>
            {mensagemErroApi(baixar.error, 'Não foi possível gerar o relatório.')}
          </Alert>
        )}
        {relatorios.map((r) => [
          <ListSubheader key={`${r.tipo}-titulo`} sx={{ lineHeight: 2.2, fontWeight: 700 }}>
            {r.titulo}
          </ListSubheader>,
          ...formatos.map((f) => (
            <MenuItem
              key={`${r.tipo}-${f.formato}`}
              disabled={baixando !== null}
              onClick={() => gerar(r.tipo, f.formato)}
              sx={{ pl: 3 }}
            >
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                {f.formato === 'pdf' ? (
                  <PictureAsPdfIcon fontSize="small" sx={{ color: 'text.secondary' }} />
                ) : (
                  <TableChartIcon fontSize="small" sx={{ color: 'text.secondary' }} />
                )}
                <Typography variant="body2">
                  {baixando === `${r.tipo}-${f.formato}` ? 'Gerando…' : f.rotulo}
                </Typography>
              </Stack>
            </MenuItem>
          )),
        ])}
      </Menu>
    </>
  )
}
