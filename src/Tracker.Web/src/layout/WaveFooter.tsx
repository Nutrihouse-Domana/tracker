import { Box } from '@mui/material'
import { nutrihouseColors } from '../theme'

/** Faixa decorativa ondulada, no espírito visual de referência — usa as cores do manual de marca. */
export function WaveFooter() {
  return (
    <Box
      sx={{
        position: 'fixed',
        left: 0,
        right: 0,
        bottom: 0,
        height: 64,
        pointerEvents: 'none',
        zIndex: 0,
        overflow: 'hidden',
      }}
    >
      <svg
        viewBox="0 0 1440 120"
        preserveAspectRatio="none"
        style={{ width: '100%', height: '100%', display: 'block' }}
      >
        <path
          d="M0,80 C240,20 480,20 720,60 C960,100 1200,100 1440,50 L1440,120 L0,120 Z"
          fill={nutrihouseColors.dourado}
          opacity={0.55}
        />
        <path
          d="M0,90 C240,50 480,90 720,80 C960,70 1200,30 1440,70 L1440,120 L0,120 Z"
          fill={nutrihouseColors.tealVerdeAzulado}
          opacity={0.6}
        />
        <path
          d="M0,100 C240,110 480,70 720,95 C960,120 1200,90 1440,100 L1440,120 L0,120 Z"
          fill={nutrihouseColors.terracota}
          opacity={0.9}
        />
      </svg>
    </Box>
  )
}
