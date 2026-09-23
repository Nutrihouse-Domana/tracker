import { useState } from 'react'
import {
  Chip,
  IconButton,
  Menu,
  MenuItem,
  Stack,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogContentText,
  DialogActions,
  Button,
} from '@mui/material'
import EditIcon from '@mui/icons-material/EditOutlined'
import type { StatusDispositivo } from '../../api/types'
import { useAtualizarStatusDispositivo } from './hooks'
import { corStatus, rotuloStatus } from './status'

/** Transições de status que a TI pode aplicar manualmente (rótulo pt-BR por destino). */
const acoesStatus: { destino: StatusDispositivo; label: string; confirmar?: boolean }[] = [
  { destino: 'EmEstoque', label: 'Devolver ao estoque' },
  { destino: 'EmManutencao', label: 'Enviar para manutenção' },
  { destino: 'Baixado', label: 'Dar baixa', confirmar: true },
]

interface Props {
  dispositivoId: string
  status: StatusDispositivo
  /** False = ativo do local (lotado na unidade, sem detentor). Habilita "Colocar em uso". */
  pertenceAUsuario: boolean
  /** Unidade que o ativo atende. Necessária para colocar um ativo de local em uso. */
  unidadeId: string | null
}

/** Chip do status atual + menu para alterá-lo manualmente (devolver ao estoque, baixa, manutenção). */
export function StatusDispositivoAcoes({ dispositivoId, status, pertenceAUsuario, unidadeId }: Props) {
  const [menuAncora, setMenuAncora] = useState<HTMLElement | null>(null)
  const [confirmar, setConfirmar] = useState<StatusDispositivo | null>(null)
  const atualizar = useAtualizarStatusDispositivo()

  const aplicar = (destino: StatusDispositivo) => {
    setMenuAncora(null)
    atualizar.mutate({ id: dispositivoId, status: destino })
  }

  // Ativo do local vira "Em uso" pela atribuição a uma unidade (não passa por detentor); a opção
  // só aparece quando ele é de local, tem unidade definida e ainda não está em uso. O backend
  // (AtualizarStatus) reforça a mesma regra.
  const podeColocarEmUso = !pertenceAUsuario && unidadeId != null && status !== 'EmUso'
  const opcoes = [
    ...(podeColocarEmUso
      ? [{ destino: 'EmUso' as StatusDispositivo, label: 'Colocar em uso' }]
      : []),
    ...acoesStatus.filter((a) => a.destino !== status),
  ]

  return (
    <>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <Chip label={rotuloStatus[status] ?? status} color={corStatus[status] ?? 'default'} />
        <IconButton
          size="small"
          aria-label="Alterar status"
          onClick={(e) => setMenuAncora(e.currentTarget)}
          disabled={atualizar.isPending}
        >
          <EditIcon fontSize="small" />
        </IconButton>
      </Stack>

      <Menu anchorEl={menuAncora} open={!!menuAncora} onClose={() => setMenuAncora(null)}>
        {opcoes.map((a) => (
          <MenuItem
            key={a.destino}
            onClick={() => (a.confirmar ? (setMenuAncora(null), setConfirmar(a.destino)) : aplicar(a.destino))}
          >
            {a.label}
          </MenuItem>
        ))}
      </Menu>

      <Dialog open={!!confirmar} onClose={() => setConfirmar(null)}>
        <DialogTitle>Dar baixa no dispositivo?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            O dispositivo será marcado como <strong>baixado</strong> e desvinculado do usuário atual. Você pode
            reverter depois devolvendo-o ao estoque.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmar(null)}>Cancelar</Button>
          <Button
            color="error"
            variant="contained"
            onClick={() => {
              const destino = confirmar!
              setConfirmar(null)
              aplicar(destino)
            }}
          >
            Dar baixa
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}
