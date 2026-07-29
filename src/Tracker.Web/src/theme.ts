import { createTheme } from '@mui/material'

// Paleta e tipografia conforme Manual de Marca Nutrihouse (2025).
export const nutrihouseColors = {
  tealVerdeAzulado: '#80B5B4',
  terracota: '#B95758',
  dourado: '#E3A941',
  bege: '#D5B897',
}

export const theme = createTheme({
  palette: {
    primary: { main: nutrihouseColors.terracota },
    secondary: { main: nutrihouseColors.tealVerdeAzulado },
    warning: { main: nutrihouseColors.dourado },
    background: { default: '#FAFAF7' },
  },
  shape: {
    borderRadius: 12,
  },
  typography: {
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
  },
  components: {
    MuiAppBar: {
      styleOverrides: {
        root: {
          backgroundColor: '#FFFFFF',
          color: '#222222',
        },
      },
    },
  },
})
