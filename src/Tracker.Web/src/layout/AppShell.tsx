import { useState } from 'react'
import {
  AppBar,
  Toolbar,
  Typography,
  Box,
  Avatar,
  Stack,
  IconButton,
  Drawer,
  List,
  ListItemButton,
  ListItemText,
  Divider,
  Menu,
  MenuItem,
} from '@mui/material'
import InventoryIcon from '@mui/icons-material/Inventory2Outlined'
import MenuIcon from '@mui/icons-material/MenuOutlined'
import ArrowDropDownIcon from '@mui/icons-material/ArrowDropDown'
import { Outlet, useNavigate, useLocation } from 'react-router-dom'
import { useMe, useLogout } from '../features/auth/useAuth'
import { WaveFooter } from './WaveFooter'
import { BarraDeBusca } from '../features/busca/BarraDeBusca'
import { nutrihouseColors } from '../theme'

type NavLink = { label: string; path: string }
type NavGroup = { label: string; itens: NavLink[] }
type NavItem = NavLink | NavGroup

const ehGrupo = (item: NavItem): item is NavGroup => 'itens' in item

const navItems: NavItem[] = [
  { label: 'Dashboard', path: '/' },
  { label: 'Dispositivos', path: '/dispositivos' },
  {
    label: 'Cadastros',
    itens: [
      { label: 'Usuários', path: '/usuarios' },
      { label: 'Unidades', path: '/unidades' },
    ],
  },
]

const agenteItem: NavLink = { label: 'Agente', path: '/agente-android' }

export function AppShell() {
  const navigate = useNavigate()
  const location = useLocation()
  const { data: usuarioAdm } = useMe()
  const logout = useLogout()
  const [menuAberto, setMenuAberto] = useState(false)
  const [cadastrosEl, setCadastrosEl] = useState<null | HTMLElement>(null)
  const [usuarioEl, setUsuarioEl] = useState<null | HTMLElement>(null)

  const handleLogout = () => {
    logout.mutate(undefined, { onSuccess: () => navigate('/login', { replace: true }) })
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

          <BarraDeBusca />

          <Box sx={{ flexGrow: 1, display: { xs: 'none', md: 'block' } }} />

          <Stack direction="row" spacing={3} sx={{ alignItems: 'center', display: { xs: 'none', sm: 'flex' } }}>
            {navItems.map((item) => {
              if (ehGrupo(item)) {
                const ativo = item.itens.some((sub) => location.pathname === sub.path)
                return (
                  <Box key={item.label}>
                    <Box
                      onClick={(e) => setCadastrosEl(e.currentTarget)}
                      sx={{
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        pt: 0.5,
                        pb: 0.5,
                        borderTop: '2px solid transparent',
                        borderBottom: '2px solid',
                        borderBottomColor: ativo ? nutrihouseColors.dourado : 'transparent',
                      }}
                    >
                      <Typography
                        variant="body2"
                        sx={{
                          fontWeight: ativo ? 700 : 500,
                          color: ativo ? nutrihouseColors.terracota : 'text.primary',
                        }}
                      >
                        {item.label.toUpperCase()}
                      </Typography>
                      <ArrowDropDownIcon
                        fontSize="small"
                        sx={{ color: ativo ? nutrihouseColors.terracota : 'text.primary' }}
                      />
                    </Box>
                    <Menu
                      anchorEl={cadastrosEl}
                      open={Boolean(cadastrosEl)}
                      onClose={() => setCadastrosEl(null)}
                      anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
                      transformOrigin={{ vertical: 'top', horizontal: 'left' }}
                    >
                      {item.itens.map((sub) => (
                        <MenuItem
                          key={sub.path}
                          selected={location.pathname === sub.path}
                          onClick={() => {
                            navigate(sub.path)
                            setCadastrosEl(null)
                          }}
                        >
                          {sub.label}
                        </MenuItem>
                      ))}
                    </Menu>
                  </Box>
                )
              }

              const ativo = location.pathname === item.path
              return (
                <Box
                  key={item.path}
                  onClick={() => navigate(item.path)}
                  sx={{
                    cursor: 'pointer',
                    pt: 0.5,
                    pb: 0.5,
                    borderTop: '2px solid transparent',
                    borderBottom: '2px solid',
                    borderBottomColor: ativo ? nutrihouseColors.dourado : 'transparent',
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
            {/* Nome + foto: o conjunto inteiro abre o menu do usuário (só em telas >= sm) */}
            <Stack
              direction="row"
              spacing={1.5}
              onClick={(e) => setUsuarioEl(e.currentTarget)}
              sx={{
                alignItems: 'center',
                cursor: 'pointer',
                borderRadius: 999,
                p: 0.5,
                display: { xs: 'none', sm: 'flex' },
                '&:hover': { bgcolor: 'action.hover' },
              }}
            >
              {usuarioAdm && (
                <Typography
                  variant="body2"
                  noWrap
                  sx={{ display: { xs: 'none', lg: 'block' }, fontWeight: 700, pl: 1 }}
                >
                  Olá, {usuarioAdm.nome}
                </Typography>
              )}
              <Avatar sx={{ bgcolor: nutrihouseColors.terracota, width: 34, height: 34 }}>
                {usuarioAdm?.nome?.[0]?.toUpperCase() ?? '?'}
              </Avatar>
            </Stack>
            {/* Nas telas pequenas o avatar não abre menu — a saída fica no drawer */}
            <Avatar
              sx={{ bgcolor: nutrihouseColors.terracota, width: 34, height: 34, display: { xs: 'flex', sm: 'none' } }}
            >
              {usuarioAdm?.nome?.[0]?.toUpperCase() ?? '?'}
            </Avatar>
            <Menu
              anchorEl={usuarioEl}
              open={Boolean(usuarioEl)}
              onClose={() => setUsuarioEl(null)}
              anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
              transformOrigin={{ vertical: 'top', horizontal: 'right' }}
            >
              {usuarioAdm && (
                <Box sx={{ px: 2, py: 1 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    {usuarioAdm.nome}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {usuarioAdm.email}
                  </Typography>
                </Box>
              )}
              <Divider />
              <MenuItem
                selected={location.pathname === agenteItem.path}
                onClick={() => {
                  setUsuarioEl(null)
                  navigate(agenteItem.path)
                }}
              >
                {agenteItem.label}
              </MenuItem>
              <Divider />
              <MenuItem
                onClick={() => {
                  setUsuarioEl(null)
                  handleLogout()
                }}
              >
                Sair
              </MenuItem>
            </Menu>
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
            {navItems.map((item) => {
              if (ehGrupo(item)) {
                return (
                  <Box key={item.label}>
                    <ListItemText
                      primary={item.label}
                      slotProps={{ primary: { variant: 'overline', color: 'text.secondary' } }}
                      sx={{ px: 2, pt: 1 }}
                    />
                    {item.itens.map((sub) => (
                      <ListItemButton
                        key={sub.path}
                        selected={location.pathname === sub.path}
                        onClick={() => irPara(sub.path)}
                        sx={{ pl: 4 }}
                      >
                        <ListItemText primary={sub.label} />
                      </ListItemButton>
                    ))}
                  </Box>
                )
              }
              return (
                <ListItemButton
                  key={item.path}
                  selected={location.pathname === item.path}
                  onClick={() => irPara(item.path)}
                >
                  <ListItemText primary={item.label} />
                </ListItemButton>
              )
            })}
          </List>
          <Divider />
          <List>
            <ListItemButton
              selected={location.pathname === agenteItem.path}
              onClick={() => irPara(agenteItem.path)}
            >
              <ListItemText primary={agenteItem.label} />
            </ListItemButton>
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
