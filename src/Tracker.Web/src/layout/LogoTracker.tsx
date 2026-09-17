import { Box, useTheme, type SxProps, type Theme } from '@mui/material'
import logoClaro from '../assets/tracker-logo-light.png'
import logoEscuro from '../assets/tracker-logo-dark.png'

interface Props {
  /** Tamanho (largura=altura) em px. */
  tamanho?: number
  sx?: SxProps<Theme>
}

/**
 * Logo do Tracker que troca conforme o tema: molduras escuras no modo claro,
 * molduras brancas no modo escuro (para contrastar com o fundo em cada caso).
 */
export function LogoTracker({ tamanho = 40, sx }: Props) {
  const escuro = useTheme().palette.mode === 'dark'
  return (
    <Box
      component="img"
      src={escuro ? logoEscuro : logoClaro}
      alt="Tracker Nutrihouse"
      sx={{
        width: tamanho,
        height: tamanho,
        borderRadius: '10px',
        objectFit: 'cover',
        flexShrink: 0,
        ...sx,
      }}
    />
  )
}
