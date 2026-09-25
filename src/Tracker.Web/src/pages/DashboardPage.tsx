import { Grid, Box, Typography, CircularProgress } from '@mui/material'
import type { SvgIconComponent } from '@mui/icons-material'
import LaptopMacIcon from '@mui/icons-material/LaptopMacOutlined'
import SmartphoneIcon from '@mui/icons-material/SmartphoneOutlined'
import MouseIcon from '@mui/icons-material/MouseOutlined'
import PrintIcon from '@mui/icons-material/PrintOutlined'
import Inventory2Icon from '@mui/icons-material/Inventory2Outlined'
import BuildIcon from '@mui/icons-material/BuildOutlined'
import BlockIcon from '@mui/icons-material/BlockOutlined'
import ArchiveIcon from '@mui/icons-material/ArchiveOutlined'
import CheckCircleIcon from '@mui/icons-material/CheckCircleOutlined'
import ArrowForwardIcon from '@mui/icons-material/ArrowForwardOutlined'
import { useNavigate } from 'react-router-dom'
import { useEstatisticasDashboard } from '../features/dashboard/hooks'
import { nutrihouseColors } from '../theme'

/** Escurece um hex por uma fração (0–1), para o gradiente do card. */
function escurecer(hex: string, fracao: number) {
  const n = parseInt(hex.slice(1), 16)
  const r = Math.max(0, (n >> 16) - Math.round(255 * fracao))
  const g = Math.max(0, ((n >> 8) & 0xff) - Math.round(255 * fracao))
  const b = Math.max(0, (n & 0xff) - Math.round(255 * fracao))
  return `rgb(${r}, ${g}, ${b})`
}

/** Converte hex em rgba com alfa, usado nas sombras coloridas. */
function comAlfa(hex: string, alfa: number) {
  const n = parseInt(hex.slice(1), 16)
  return `rgba(${n >> 16}, ${(n >> 8) & 0xff}, ${n & 0xff}, ${alfa})`
}

interface CartaoProps {
  titulo: string
  valor: number | undefined
  cor: string
  Icone: SvgIconComponent
  legenda?: string
  compacto?: boolean
  onClick?: () => void
}

function Cartao({ titulo, valor, cor, Icone, legenda, compacto, onClick }: CartaoProps) {
  return (
    <Box
      onClick={onClick}
      sx={{
        position: 'relative',
        overflow: 'hidden',
        color: '#fff',
        borderRadius: '18px',
        px: 2.5,
        py: compacto ? 1.75 : 2,
        height: compacto ? 96 : 104,
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        background: `linear-gradient(135deg, ${cor} 0%, ${escurecer(cor, 0.14)} 100%)`,
        boxShadow: `0 8px 20px -8px ${comAlfa(cor, 0.55)}`,
        cursor: onClick ? 'pointer' : 'default',
        transition: 'transform 0.18s ease, box-shadow 0.18s ease',
        '&:hover': onClick
          ? { transform: 'translateY(-4px)', boxShadow: `0 16px 30px -8px ${comAlfa(cor, 0.65)}` }
          : undefined,
      }}
    >
      {/* Ícone grande em marca d'água ao fundo */}
      <Icone
        sx={{
          position: 'absolute',
          right: -14,
          bottom: -14,
          fontSize: compacto ? 82 : 100,
          opacity: 0.16,
          transform: 'rotate(-8deg)',
          pointerEvents: 'none',
        }}
      />

      <Box sx={{ position: 'relative' }}>
        <Typography sx={{ fontWeight: 800, fontSize: compacto ? 26 : 32, lineHeight: 1 }}>
          {valor ?? '—'}
        </Typography>
        <Typography variant="body2" sx={{ fontWeight: 600, opacity: 0.95 }}>
          {titulo}
        </Typography>
        {legenda && (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mt: 0.5, opacity: 0.85 }}>
            <Typography variant="caption" sx={{ fontWeight: 600 }}>
              {legenda}
            </Typography>
            <ArrowForwardIcon sx={{ fontSize: 14 }} />
          </Box>
        )}
      </Box>
    </Box>
  )
}

export function DashboardPage() {
  const { data, isLoading } = useEstatisticasDashboard()
  const navigate = useNavigate()

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
        <CircularProgress />
      </Box>
    )
  }

  const porTipo: (Omit<CartaoProps, 'onClick'> & { tipo: string })[] = [
    { titulo: 'Notebooks', valor: data?.totalNotebooks, cor: nutrihouseColors.terracota, Icone: LaptopMacIcon, legenda: 'Ver todos', tipo: 'Notebook' },
    { titulo: 'Mobiles', valor: data?.totalMobiles, cor: nutrihouseColors.tealVerdeAzulado, Icone: SmartphoneIcon, legenda: 'Ver todos', tipo: 'Mobile' },
    { titulo: 'Periféricos', valor: data?.totalPerifericos, cor: nutrihouseColors.dourado, Icone: MouseIcon, legenda: 'Ver todos', tipo: 'Periferico' },
    { titulo: 'Impressoras', valor: data?.totalImpressoras, cor: '#9C8566', Icone: PrintIcon, legenda: 'Ver todas', tipo: 'Impressora' },
  ]

  const porStatus: (Omit<CartaoProps, 'onClick'> & { status: string })[] = [
    { titulo: 'Em uso', valor: data?.emUso, cor: '#5B8C5A', Icone: CheckCircleIcon, compacto: true, status: 'EmUso' },
    { titulo: 'Em estoque', valor: data?.emEstoque, cor: '#C9A24B', Icone: Inventory2Icon, compacto: true, status: 'EmEstoque' },
    { titulo: 'Em manutenção', valor: data?.emManutencao, cor: nutrihouseColors.dourado, Icone: BuildIcon, compacto: true, status: 'EmManutencao' },
    { titulo: 'Bloqueados', valor: data?.bloqueados, cor: '#8B3A3A', Icone: BlockIcon, compacto: true, status: 'Bloqueado' },
    { titulo: 'Baixados', valor: data?.baixados, cor: '#6E6E6E', Icone: ArchiveIcon, compacto: true, status: 'Baixado' },
  ]

  return (
    <>
      <Typography variant="h5" gutterBottom>
        Visão geral
      </Typography>

      <Typography variant="subtitle1" sx={{ mt: 2, mb: 1.5 }}>
        Por tipo de dispositivo
      </Typography>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 3, mb: 4, justifyContent: 'center' }}>
        {porTipo.map((c) => (
          <Box key={c.titulo} sx={{ flex: '1 1 220px', maxWidth: { xs: '100%', sm: 300 } }}>
            <Cartao {...c} onClick={() => navigate(`/dispositivos?tipo=${c.tipo}`)} />
          </Box>
        ))}
      </Box>

      <Typography variant="subtitle1" sx={{ mb: 1.5 }}>
        Por status
      </Typography>
      <Grid container spacing={2}>
        {porStatus.map((c) => (
          <Grid key={c.titulo} size={{ xs: 6, sm: 4, md: 12 / 5 }}>
            <Cartao {...c} onClick={() => navigate(`/dispositivos?status=${c.status}`)} />
          </Grid>
        ))}
      </Grid>
    </>
  )
}
