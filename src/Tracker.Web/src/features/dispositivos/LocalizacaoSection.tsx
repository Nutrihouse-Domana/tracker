import { Box, Typography, Stack } from '@mui/material'
import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet'
import { Icon } from 'leaflet'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'
import { useHistoricoLocalizacao } from './hooks'

// Corrige o ícone padrão do Leaflet (que quebra com bundlers como o Vite).
const icone = new Icon({
  iconUrl: markerIcon,
  shadowUrl: markerShadow,
  iconSize: [25, 41],
  iconAnchor: [12, 41],
})

interface Props {
  dispositivoId: string
}

export function LocalizacaoSection({ dispositivoId }: Props) {
  const { data: historico } = useHistoricoLocalizacao(dispositivoId)
  const ultima = historico?.[0]

  if (!ultima) {
    return (
      <Typography variant="body2" color="text.secondary" sx={{ p: 1 }}>
        Nenhuma localização reportada ainda. O agente envia a posição no heartbeat.
      </Typography>
    )
  }

  return (
    <Stack spacing={2} sx={{ mt: 1 }}>
      <Box>
        <Typography variant="body2">
          <strong>Última localização:</strong> {ultima.enderecoAproximado ?? '—'}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {new Date(ultima.capturadoEm).toLocaleString('pt-BR')} · precisão ~
          {ultima.precisaoMetros ? `${Math.round(ultima.precisaoMetros)} m` : 'desconhecida'} · origem: {ultima.origem}
        </Typography>
      </Box>

      <Box sx={{ height: 360, borderRadius: 2, overflow: 'hidden' }}>
        <MapContainer
          center={[ultima.latitude, ultima.longitude]}
          zoom={11}
          style={{ height: '100%', width: '100%' }}
        >
          <TileLayer
            attribution='&copy; OpenStreetMap'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <Marker position={[ultima.latitude, ultima.longitude]} icon={icone}>
            <Popup>{ultima.enderecoAproximado ?? 'Dispositivo'}</Popup>
          </Marker>
        </MapContainer>
      </Box>
    </Stack>
  )
}
