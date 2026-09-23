import { Avatar, Box, Chip, Divider, Paper, Stack, Typography } from '@mui/material'
import { useMe } from '../features/auth/useAuth'
import { PERMISSOES, usePermissoes } from '../features/auth/permissoes'
import { nutrihouseColors } from '../theme'

/** Rótulos legíveis das permissões, para o usuário ver o que a conta dele pode fazer. */
const rotuloPermissao: Record<string, string> = {
  [PERMISSOES.dispositivosVer]: 'Ver dispositivos',
  [PERMISSOES.dispositivosCadastrar]: 'Cadastrar/editar',
  [PERMISSOES.dispositivosMovimentar]: 'Movimentar',
  [PERMISSOES.dispositivosExcluir]: 'Arquivar',
  [PERMISSOES.dispositivosExcluirDefinitivo]: 'Excluir definitivo',
  [PERMISSOES.dispositivosBloquear]: 'Bloquear/desbloquear',
  [PERMISSOES.agentePublicar]: 'Publicar APK do agente',
  [PERMISSOES.usuariosGerir]: 'Gerir usuários',
  [PERMISSOES.unidadesGerir]: 'Gerir unidades',
  [PERMISSOES.auditoriaVer]: 'Ver auditoria',
  [PERMISSOES.acessosGerir]: 'Gerir acessos',
}

/**
 * Conta do próprio usuário logado. Não exige permissão nenhuma — mostra só quem ele é e o que a
 * conta pode fazer. Os relatórios do inventário ficam no menu da conta (MenuRelatorios), não aqui.
 */
export function PerfilPage() {
  const { data: usuario } = useMe()
  const { lista } = usePermissoes()

  return (
    <>
      <Typography variant="h5" sx={{ mb: 2 }}>
        Perfil
      </Typography>

      <Paper sx={{ p: 3 }}>
        <Stack direction="row" spacing={2} sx={{ alignItems: 'center', mb: 2 }}>
          <Avatar sx={{ bgcolor: nutrihouseColors.terracota, width: 56, height: 56, fontSize: 24 }}>
            {usuario?.nome?.[0]?.toUpperCase() ?? '?'}
          </Avatar>
          <Box>
            <Typography variant="h6">{usuario?.nome ?? '—'}</Typography>
            <Typography variant="body2" color="text.secondary">
              {usuario?.email ?? '—'}
            </Typography>
          </Box>
        </Stack>

        <Divider sx={{ mb: 2 }} />

        <Typography variant="subtitle2" sx={{ mb: 1 }}>
          Permissões desta conta
        </Typography>
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
          {lista.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              Nenhuma permissão atribuída.
            </Typography>
          ) : (
            lista.map((p) => (
              <Chip key={p} size="small" variant="outlined" label={rotuloPermissao[p] ?? p} />
            ))
          )}
        </Stack>
      </Paper>
    </>
  )
}
