import { Routes, Route } from 'react-router-dom'
import { AppShell } from './layout/AppShell'
import { DashboardPage } from './pages/DashboardPage'
import { DispositivosPage } from './pages/DispositivosPage'
import { DispositivoDetalhePage } from './pages/DispositivoDetalhePage'
import { UsuariosPage } from './pages/UsuariosPage'
import { UnidadesPage } from './pages/UnidadesPage'
import { AgenteAndroidPage } from './pages/AgenteAndroidPage'
import { AcessosPage } from './pages/AcessosPage'
import { AuditoriaPage } from './pages/AuditoriaPage'
import { SemAcessoPage } from './pages/SemAcessoPage'
import { PerfilPage } from './pages/PerfilPage'
import { LoginPage } from './pages/LoginPage'
import { AuthGuard } from './features/auth/AuthGuard'
import { PermissaoRoute, PERMISSOES } from './features/auth/RequirePermissao'
import { EXIGENCIA_UNIDADES, EXIGENCIA_USUARIOS } from './features/auth/permissoes'

function App() {
  return (
    <Routes>
      <Route path="login" element={<LoginPage />} />
      <Route element={<AuthGuard />}>
        <Route element={<AppShell />}>
          {/* Cada página fica atrás da sua permissão; sem ela, o guard redireciona para a primeira
              página que o usuário pode ver. "Ocultar, não desabilitar" também vale por URL. */}
          <Route element={<PermissaoRoute permissao={PERMISSOES.dispositivosVer} />}>
            <Route index element={<DashboardPage />} />
            <Route path="dispositivos" element={<DispositivosPage />} />
            <Route path="dispositivos/:id" element={<DispositivoDetalhePage />} />
          </Route>
          {/* Usuários/Unidades: gestão + dispositivos.ver (os GETs da listagem pedem esta). */}
          <Route element={<PermissaoRoute permissao={EXIGENCIA_USUARIOS} />}>
            <Route path="usuarios" element={<UsuariosPage />} />
          </Route>
          <Route element={<PermissaoRoute permissao={EXIGENCIA_UNIDADES} />}>
            <Route path="unidades" element={<UnidadesPage />} />
          </Route>
          <Route element={<PermissaoRoute permissao={PERMISSOES.agentePublicar} />}>
            <Route path="agente-android" element={<AgenteAndroidPage />} />
          </Route>
          <Route element={<PermissaoRoute permissao={PERMISSOES.acessosGerir} />}>
            <Route path="acessos" element={<AcessosPage />} />
          </Route>
          <Route element={<PermissaoRoute permissao={PERMISSOES.auditoriaVer} />}>
            <Route path="auditoria" element={<AuditoriaPage />} />
          </Route>
          {/* Perfil é a conta do próprio usuário logado: não exige permissão nenhuma.
              A seção de relatórios dentro dela é que checa 'dispositivos.ver'. */}
          <Route path="perfil" element={<PerfilPage />} />
          <Route path="sem-acesso" element={<SemAcessoPage />} />
        </Route>
      </Route>
    </Routes>
  )
}

export default App
