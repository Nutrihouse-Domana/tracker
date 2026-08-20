import { Box, Typography, Stack } from '@mui/material'
import LockPersonIcon from '@mui/icons-material/LockPersonOutlined'

/** Exibida quando o usuário não tem permissão para nenhuma página de navegação. */
export function SemAcessoPage() {
  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
      <Stack spacing={2} sx={{ alignItems: 'center', maxWidth: 420, textAlign: 'center' }}>
        <LockPersonIcon sx={{ fontSize: 64, color: 'text.disabled' }} />
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          Sem permissões
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Seu login ainda não tem permissão para acessar nenhuma área do sistema. Fale com um
          administrador para liberar os acessos necessários.
        </Typography>
      </Stack>
    </Box>
  )
}
