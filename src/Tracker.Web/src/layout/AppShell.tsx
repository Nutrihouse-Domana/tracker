import { useState } from 'react'
import {
  AppBar,
  Toolbar,
  Typography,
  Box,
  Button,
  InputBase,
  Avatar,
  Stack,
  IconButton,
  Drawer,
  List,
  ListItemButton,
  ListItemText,
  Divider,
} from '@mui/material'
import SearchIcon from '@mui/icons-material/SearchOutlined'
import InventoryIcon from '@mui/icons-material/Inventory2Outlined'
import MenuIcon from '@mui/icons-material/MenuOutlined'
import { Outlet, useNavigate, useLocation } from 'react-router-dom'
import { useMe, useLogout } from '../features/auth/useAuth'
import { WaveFooter } from './WaveFooter'
import { nutrihouseColors } from '../theme'

const navItems = [
  { label: 'Dashboard', path: '/' },
  { label: 'Dispositivos', path: '/dispositivos' },
  { label: 'Usuários', path: '/usuarios' },
]

export function AppShell() {
  const navigate = useNavigate()
  const location = useLocation()
  const { data: usuarioAdm } = useMe()
  const logout = useLogout()
  const [busca, setBusca] = useState('')
  const [menuAberto, setMenuAberto] = useState(false)

  const handleLogout = () => {
    logout.mutate(undefined, { onSuccess: () => navigate('/login', { replace: true }) })
  }

  const handleBuscar = (e: React.FormEvent) => {
    e.preventDefault()
    if (!busca.trim()) return
    navigate(`/dispositivos?q=${encodeURIComponent(busca.trim())}`)
  }

  const irPara = (path: string) => {
    navigate(path)
    setMenuAberto(false)
  }

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <AppBar position="sticky" elevation={0} sx={{ borderBottom: '1px solid', borderColor: 'divider' }}>
        <Toolbar sx={{ gap: { xs: 1, md: 3 }, py: 1 }}>
          {/* Botão de menu — só aparece em telas pequenas */}
          <IconButton
            edge="start"
            onClick={() => setMenuAberto(true)}
            sx={{ display: { xs: 'inline-flex', sm: 'none' } }}
          >
            <MenuIcon />
          </IconButton>

          <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: 'center', cursor: 'pointer' }}
            onClick={() => navigate('/')}
          >
            <Box
              sx={{
                width: 40,
                height: 40,
                borderRadius: '10px',
                bgcolor: nutrihouseColors.terracota,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                flexShrink: 0,
              }}
            >
              <InventoryIcon sx={{ color: '#fff' }} />
            </Box>
            <Typography variant="h6" noWrap sx={{ fontWeight: 700, display: { xs: 'none', sm: 'block' } }}>
              Tracker <Box component="span" sx={{ color: nutrihouseColors.terracota }}>Nutrihouse</Box>
            </Typography>
          </Stack>

          <Box
            component="form"
            onSubmit={handleBuscar}
            sx={{
              display: { xs: 'flex', md: 'flex' },
              alignItems: 'center',
              bgcolor: 'grey.100',
              borderRadius: 999,
              px: 2,
              py: 0.5,
              flexGrow: 1,
              maxWidth: { xs: 'none', md: 360 },
            }}
          >
            <InputBase
              placeholder="Buscar dispositivos..."
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              sx={{ flexGrow: 1, fontSize: 14 }}
            />
            <SearchIcon fontSize="small" color="action" />
          </Box>

          <Box sx={{ flexGrow: 1, display: { xs: 'none', md: 'block' } }} />

          <Stack direction="row" spacing={3} sx={{ alignItems: 'center', display: { xs: 'none', sm: 'flex' } }}>
            {navItems.map((item) => {
              const ativo = location.pathname === item.path
              return (
                <Box
                  key={item.path}
                  onClick={() => navigate(item.path)}
                  sx={{
                    cursor: 'pointer',
                    pb: 0.5,
                    borderBottom: ativo ? '2px solid' : '2px solid transparent',
                    borderColor: ativo ? nutrihouseColors.dourado : 'transparent',
                  }}
                >
                  <Typography
                    variant="body2"
                    sx={{ fontWeight: ativo ? 700 : 500, color: ativo ? nutrihouseColors.terracota : 'text.primary' }}
                  >
                    {item.label.toUpperCase()}
                  </Typography>
                </Box>
              )
            })}
          </Stack>

          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flexShrink: 0 }}>
            {usuarioAdm && (
              <Typography
                variant="body2"
                noWrap
                sx={{
                  display: { xs: 'none', lg: 'block' },
                  fontWeight: 700,
                  pb: 0.5,
                  borderBottom: '2px solid transparent',
                }}
              >
                Olá, {usuarioAdm.nome}
              </Typography>
            )}
            <Avatar sx={{ bgcolor: nutrihouseColors.terracota, width: 34, height: 34 }}>
              {usuarioAdm?.nome?.[0]?.toUpperCase() ?? '?'}
            </Avatar>
            <Button size="small" onClick={handleLogout} sx={{ display: { xs: 'none', sm: 'inline-flex' } }}>
              Sair
            </Button>
          </Stack>
        </Toolbar>
      </AppBar>

      {/* Menu lateral para telas pequenas */}
      <Drawer anchor="left" open={menuAberto} onClose={() => setMenuAberto(false)}>
        <Box sx={{ width: 240 }}>
          {usuarioAdm && (
            <Box sx={{ p: 2 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                {usuarioAdm.nome}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {usuarioAdm.email}
              </Typography>
            </Box>
          )}
          <Divider />
          <List>
            {navItems.map((item) => (
              <ListItemButton
                key={item.path}
                selected={location.pathname === item.path}
                onClick={() => irPara(item.path)}
              >
                <ListItemText primary={item.label} />
              </ListItemButton>
            ))}
          </List>
          <Divider />
          <List>
            <ListItemButton onClick={handleLogout}>
              <ListItemText primary="Sair" />
            </ListItemButton>
          </List>
        </Box>
      </Drawer>

      <Box
        component="main"
        sx={{ flexGrow: 1, p: { xs: 2, md: 3 }, pb: { xs: 10, md: 12 }, position: 'relative', zIndex: 1 }}
      >
        <Box sx={{ maxWidth: 1400, mx: 'auto', width: '100%' }}>
          <Outlet />
        </Box>
      </Box>

      <WaveFooter />
    </Box>
  )
}
