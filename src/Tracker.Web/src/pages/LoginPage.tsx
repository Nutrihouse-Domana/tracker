import { useState } from 'react'
import { Box, Button, Paper, TextField, Typography, Alert } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { useLogin } from '../features/auth/useAuth'

export function LoginPage() {
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const login = useLogin()
  const navigate = useNavigate()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    login.mutate(
      { email, senha },
      { onSuccess: () => navigate('/', { replace: true }) },
    )
  }

  return (
    <Box
      sx={{
        display: 'flex',
        minHeight: '100vh',
        alignItems: 'center',
        justifyContent: 'center',
        bgcolor: 'background.default',
      }}
    >
      <Paper component="form" onSubmit={handleSubmit} sx={{ p: 4, width: 360 }}>
        <Typography variant="h5" gutterBottom>
          Tracker Nutrihouse
        </Typography>
        <Typography variant="body2" color="text.secondary" gutterBottom>
          Acesso restrito à equipe de TI
        </Typography>

        {login.isError && (
          <Alert severity="error" sx={{ mt: 2 }}>
            Credenciais inválidas.
          </Alert>
        )}

        <TextField
          label="Email"
          type="email"
          fullWidth
          margin="normal"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
        />
        <TextField
          label="Senha"
          type="password"
          fullWidth
          margin="normal"
          value={senha}
          onChange={(e) => setSenha(e.target.value)}
          required
        />
        <Button type="submit" variant="contained" fullWidth sx={{ mt: 2 }} disabled={login.isPending}>
          {login.isPending ? 'Entrando...' : 'Entrar'}
        </Button>
      </Paper>
    </Box>
  )
}
