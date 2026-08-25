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
import MenuIcon from '@mui/icons-material/MenuOutlined'
import ArrowDropDownIcon from '@mui/icons-material/ArrowDropDown'
import { Outlet, useNavigate, useLocation } from 'react-router-dom'
import { useMe, useLogout } from '../features/auth/useAuth'
import { usePermissoes, PERMISSOES, type Permissao } from '../features/auth/permissoes'
import { WaveFooter } from './WaveFooter'
import { BarraDeBusca } from '../features/busca/BarraDeBusca'
import logoTracker from '../assets/tracker-logo.jpg'
import { nutrihouseColors } from '../theme'

type NavLink = { label: string; path: string; perm: Permissao }
type NavGroup = { label: string; itens: NavLink[] }
type NavItem = NavLink | NavGroup

const ehGrupo = (item: NavItem): item is NavGroup => 'itens' in item

// Toda a navegação com a permissão exigida por item. A filtragem por permissão acontece no render:
// o que o usuário não pode ver, ele não vê (princípio "ocultar, não desabilitar").
const navItemsBase: NavItem[] = [
  { label: 'Dashboard', path: '/', perm: PERMISSOES.dispositivosVer },
  { label: 'Dispositivos', path: '/dispositivos', perm: PERMISSOES.dispositivosVer },
  {
    label: 'Cadastros',
    itens: [
      { label: 'Usuários', path: '/usuarios', perm: PERMISSOES.usuariosGerir },
      { label: 'Unidades', path: '/unidades', perm: PERMISSOES.unidadesGerir },
    ],
  },
]

// Itens do menu da conta (aberto pela foto/avatar): agente e administração ficam aqui, não no topo.
const menuContaItens: NavLink[] = [
  { label: 'Agente', path: '/agente-android', perm: PERMISSOES.agentePublicar },
  { label: 'Acessos', path: '/acessos', perm: PERMISSOES.acessosGerir },
  { label: 'Auditoria', path: '/auditoria', perm: PERMISSOES.auditoriaVer },
]

/** Remove itens sem permissão e grupos que ficaram vazios. */
function filtrarNav(itens: NavItem[], tem: (p: Permissao) => boolean): NavItem[] {
  return itens
    .map((item) => {
      if (ehGrupo(item)) {
        const visiveis = item.itens.filter((sub) => tem(sub.perm))
        return visiveis.length > 0 ? { ...item, itens: visiveis } : null
      }
      return tem(item.perm) ? item : null
    })
    .filter((item): item is NavItem => item !== null)
}

export function AppShell() {
  const navigate = useNavigate()
  const location = useLocation()
  const { data: usuarioAdm } = useMe()
  const { tem } = usePermissoes()
  const logout = useLogout()
  const [menuAberto, setMenuAberto] = useState(false)
  // Menu de grupo aberto (Cadastros/Administração): guarda o label e o elemento âncora, já que há
  // mais de um grupo e eles não podem compartilhar a mesma âncora.
  const [grupoMenu, setGrupoMenu] = useState<{ label: string; el: HTMLElement } | null>(null)
  const [usuarioEl, setUsuarioEl] = useState<null | HTMLElement>(null)

  const navItems = filtrarNav(navItemsBase, tem)
  const contaItens = menuContaItens.filter((i) => tem(i.perm))
  const podeBuscar = tem(PERMISSOES.dispositivosVer)

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
              component="img"
              src={logoTracker}
              alt="Tracker Nutrihouse"
              sx={{
                width: 40,
                height: 40,
                borderRadius: '10px',
                objectFit: 'cover',
                flexShrink: 0,
              }}
            />
            <Typography variant="h6" noWrap sx={{ fontWeight: 700, display: { xs: 'none', sm: 'block' } }}>
              Tracker <Box component="span" sx={{ color: nutrihouseColors.terracota }}>Nutrihouse</Box>
            </Typography>
          </Stack>

          {podeBuscar && <BarraDeBusca />}

          <Box sx={{ flexGrow: 1, display: { xs: 'none', md: 'block' } }} />

          <Stack direction="row" spacing={3} sx={{ alignItems: 'center', display: { xs: 'none', sm: 'flex' } }}>
            {navItems.map((item) => {
              if (ehGrupo(item)) {
                const ativo = item.itens.some((sub) => location.pathname === sub.path)
                return (
                  <Box key={item.label}>
                    <Box
                      onClick={(e) => setGrupoMenu({ label: item.label, el: e.currentTarget })}
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
                      anchorEl={grupoMenu?.el}
                      open={grupoMenu?.label === item.label}
                      onClose={() => setGrupoMenu(null)}
                      anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
                      transformOrigin={{ vertical: 'top', horizontal: 'left' }}
                    >
                      {item.itens.map((sub) => (
                        <MenuItem
                          key={sub.path}
                          selected={location.pathname === sub.path}
                          onClick={() => {
                            navigate(sub.path)
                            setGrupoMenu(null)
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
              {contaItens.length > 0 && <Divider />}
              {contaItens.map((item) => (
                <MenuItem
                  key={item.path}
                  selected={location.pathname === item.path}
                  onClick={() => {
                    setUsuarioEl(null)
                    navigate(item.path)
                  }}
                >
                  {item.label}
                </MenuItem>
              ))}
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
            {contaItens.map((item) => (
              <ListItemButton
                key={item.path}
                selected={location.pathname === item.path}
                onClick={() => irPara(item.path)}
              >
                <ListItemText primary={item.label} />
              </ListItemButton>
            ))}
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
