import { Routes, Route } from 'react-router-dom'
import { AppShell } from './layout/AppShell'
import { DashboardPage } from './pages/DashboardPage'
import { DispositivosPage } from './pages/DispositivosPage'
import { DispositivoDetalhePage } from './pages/DispositivoDetalhePage'
import { UsuariosPage } from './pages/UsuariosPage'
import { UnidadesPage } from './pages/UnidadesPage'
import { LoginPage } from './pages/LoginPage'
import { AuthGuard } from './features/auth/AuthGuard'

function App() {
  return (
    <Routes>
      <Route path="login" element={<LoginPage />} />
      <Route element={<AuthGuard />}>
        <Route element={<AppShell />}>
          <Route index element={<DashboardPage />} />
          <Route path="dispositivos" element={<DispositivosPage />} />
          <Route path="dispositivos/:id" element={<DispositivoDetalhePage />} />
          <Route path="usuarios" element={<UsuariosPage />} />
          <Route path="unidades" element={<UnidadesPage />} />
        </Route>
      </Route>
    </Routes>
  )
}

export default App
