import { createTheme, type Theme } from '@mui/material'

// Paleta e tipografia conforme Manual de Marca Nutrihouse (2025).
export const nutrihouseColors = {
  tealVerdeAzulado: '#80B5B4',
  terracota: '#B95758',
  dourado: '#E3A941',
  bege: '#D5B897',
}

export type ModoTema = 'light' | 'dark'

// Tipografia é a mesma nos dois modos (só as cores mudam).
const tipografia = {
  // Regra do manual: Poppins para peças estilizáveis; Arial como fallback
  // em plataformas onde não é possível customizar a tipografia.
  fontFamily: '"Poppins", Arial, sans-serif',
  h1: { fontFamily: '"Poppins", Arial, sans-serif', fontWeight: 700 },
  h2: { fontFamily: '"Poppins", Arial, sans-serif', fontWeight: 700 },
  h3: { fontFamily: '"Poppins", Arial, sans-serif', fontWeight: 700 },
  h4: { fontFamily: '"Poppins", Arial, sans-serif', fontWeight: 700 },
  h5: { fontFamily: '"Poppins", Arial, sans-serif', fontWeight: 700 },
  h6: { fontWeight: 600 },
  subtitle1: { fontWeight: 600 },
  subtitle2: { fontWeight: 600 },
  body1: { fontWeight: 400 },
  body2: { fontWeight: 400 },
} as const

/**
 * Constrói o tema no modo pedido. A UI já usa tokens do tema
 * (`text.primary`, `background.default`, `divider`, `Paper`...), então trocar
 * o `mode` da paleta basta para a maior parte das telas; só a `AppBar` fixava
 * cor e é ajustada aqui por modo.
 */
export function criarTema(modo: ModoTema): Theme {
  const claro = modo === 'light'
  return createTheme({
    palette: {
      mode: modo,
      // No escuro a terracota pura fica com pouco contraste sobre fundo escuro;
      // clareamos só a variante primária usada como cor de texto/realce.
      primary: { main: claro ? nutrihouseColors.terracota : '#D98486' },
      secondary: { main: nutrihouseColors.tealVerdeAzulado },
      warning: { main: nutrihouseColors.dourado },
      background: claro
        ? { default: '#FAFAF7', paper: '#FFFFFF' }
        : { default: '#121212', paper: '#1E1E1E' },
    },
    shape: {
      borderRadius: 12,
    },
    typography: tipografia,
    components: {
      MuiAppBar: {
        styleOverrides: {
          root: {
            backgroundColor: claro ? '#FFFFFF' : '#1E1E1E',
            color: claro ? '#222222' : '#F5F5F5',
          },
        },
      },
      // No escuro o MUI clareia as cores semânticas e os chips preenchidos ficam pastel
      // (verde/vermelho lavados). Escurecemos os chips coloridos com texto branco — só no escuro.
      ...(claro
        ? {}
        : {
            MuiChip: {
              styleOverrides: {
                root: {
                  '&.MuiChip-filled.MuiChip-colorSuccess': { backgroundColor: '#2E7D32', color: '#FFFFFF' },
                  '&.MuiChip-filled.MuiChip-colorPrimary': { backgroundColor: '#8C4142', color: '#FFFFFF' },
                  '&.MuiChip-filled.MuiChip-colorError': { backgroundColor: '#B23B3B', color: '#FFFFFF' },
                },
              },
            },
            // Botões contained também ficam pastel no escuro (usam o primary clareado). Escurecemos
            // o fundo com texto branco, mantendo o primary claro para texto/realce fora do botão.
            MuiButton: {
              styleOverrides: {
                root: {
                  '&.MuiButton-contained.MuiButton-colorPrimary': {
                    backgroundColor: '#8C4142',
                    color: '#FFFFFF',
                    '&:hover': { backgroundColor: '#763638' },
                  },
                  '&.MuiButton-contained.MuiButton-colorError': {
                    backgroundColor: '#B23B3B',
                    color: '#FFFFFF',
                    '&:hover': { backgroundColor: '#8F2F2F' },
                  },
                  '&.MuiButton-contained.MuiButton-colorSuccess': {
                    backgroundColor: '#2E7D32',
                    color: '#FFFFFF',
                    '&:hover': { backgroundColor: '#25612A' },
                  },
                },
              },
            },
          }),
    },
  })
}

// Tema claro padrão — mantido para usos fora do provider (ex.: testes).
export const theme = criarTema('light')
