import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { ThemeProvider, CssBaseline } from '@mui/material'
import { criarTema, type ModoTema } from '../../theme'

const CHAVE_STORAGE = 'tracker.tema'

type ModoTemaContexto = {
  modo: ModoTema
  alternar: () => void
}

const ModoTemaContext = createContext<ModoTemaContexto | null>(null)

/** Lê a escolha salva; sem ela, segue a preferência do sistema operacional. */
function modoInicial(): ModoTema {
  try {
    const salvo = localStorage.getItem(CHAVE_STORAGE)
    if (salvo === 'light' || salvo === 'dark') return salvo
  } catch {
    // localStorage indisponível (modo privado etc.) — cai no padrão do SO.
  }
  const prefereEscuro =
    typeof window !== 'undefined' && window.matchMedia?.('(prefers-color-scheme: dark)').matches
  return prefereEscuro ? 'dark' : 'light'
}

/**
 * Provê o modo (claro/escuro) para toda a árvore e já monta o ThemeProvider do
 * MUI + CssBaseline. A escolha do usuário é persistida em localStorage; sem
 * escolha, respeita o esquema do sistema.
 */
export function ModoTemaProvider({ children }: { children: ReactNode }) {
  const [modo, setModo] = useState<ModoTema>(modoInicial)

  useEffect(() => {
    try {
      localStorage.setItem(CHAVE_STORAGE, modo)
    } catch {
      // Sem persistência: mantém apenas em memória nesta sessão.
    }
    // Mantém inputs nativos (date picker, scrollbar) coerentes com o tema.
    document.documentElement.style.colorScheme = modo
  }, [modo])

  const valor = useMemo<ModoTemaContexto>(
    () => ({ modo, alternar: () => setModo((m) => (m === 'light' ? 'dark' : 'light')) }),
    [modo],
  )

  const tema = useMemo(() => criarTema(modo), [modo])

  return (
    <ModoTemaContext.Provider value={valor}>
      <ThemeProvider theme={tema}>
        <CssBaseline />
        {children}
      </ThemeProvider>
    </ModoTemaContext.Provider>
  )
}

/** Acessa o modo atual e a função de alternância. */
export function useModoTema(): ModoTemaContexto {
  const ctx = useContext(ModoTemaContext)
  if (!ctx) throw new Error('useModoTema deve ser usado dentro de <ModoTemaProvider>')
  return ctx
}
