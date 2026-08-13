import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { dispositivosApi } from './api'
import type {
  AtribuirDispositivoRequest,
  CriarComentarioRequest,
  CriarNotebookRequest,
  CriarPerifericoRequest,
  CriarReparoRequest,
  CriarMobileRequest,
  StatusDispositivo,
  StatusReparo,
  TipoComandoRemoto,
  TipoDispositivo,
} from '../../api/types'

const KEY = ['dispositivos']

export function useDispositivos(filtros?: { tipo?: TipoDispositivo; status?: StatusDispositivo }) {
  return useQuery({
    queryKey: [...KEY, filtros ?? {}],
    queryFn: () => dispositivosApi.listar(filtros),
  })
}

export function useDispositivo(id: string | undefined) {
  return useQuery({
    queryKey: [...KEY, id],
    queryFn: () => dispositivosApi.obter(id!),
    enabled: !!id,
  })
}

export function useHistoricoMovimentacao(dispositivoId: string | undefined) {
  return useQuery({
    queryKey: [...KEY, dispositivoId, 'historico'],
    queryFn: () => dispositivosApi.historicoMovimentacao(dispositivoId!),
    enabled: !!dispositivoId,
  })
}

function useInvalidateDispositivos() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: KEY })
}

export function useCriarNotebook() {
  const invalidate = useInvalidateDispositivos()
  return useMutation({
    mutationFn: (data: CriarNotebookRequest) => dispositivosApi.criarNotebook(data),
    onSuccess: invalidate,
  })
}

export function useCriarMobile() {
  const invalidate = useInvalidateDispositivos()
  return useMutation({
    mutationFn: (data: CriarMobileRequest) => dispositivosApi.criarMobile(data),
    onSuccess: invalidate,
  })
}

export function useCriarPeriferico() {
  const invalidate = useInvalidateDispositivos()
  return useMutation({
    mutationFn: (data: CriarPerifericoRequest) => dispositivosApi.criarPeriferico(data),
    onSuccess: invalidate,
  })
}

export function useAtualizarStatusDispositivo() {
  const invalidate = useInvalidateDispositivos()
  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: StatusDispositivo }) =>
      dispositivosApi.atualizarStatus(id, status),
    onSuccess: invalidate,
  })
}

export function useAtribuirDispositivo() {
  const invalidate = useInvalidateDispositivos()
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: AtribuirDispositivoRequest }) =>
      dispositivosApi.atribuir(id, data),
    onSuccess: invalidate,
  })
}

export function useReparos(dispositivoId: string | undefined) {
  return useQuery({
    queryKey: [...KEY, dispositivoId, 'reparos'],
    queryFn: () => dispositivosApi.listarReparos(dispositivoId!),
    enabled: !!dispositivoId,
  })
}

export function useCriarReparo(dispositivoId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: CriarReparoRequest) => dispositivosApi.criarReparo(dispositivoId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...KEY, dispositivoId, 'reparos'] })
      queryClient.invalidateQueries({ queryKey: KEY })
    },
  })
}

export function useAtualizarStatusReparo(dispositivoId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ reparoId, status }: { reparoId: string; status: StatusReparo }) =>
      dispositivosApi.atualizarStatusReparo(dispositivoId, reparoId, status),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...KEY, dispositivoId, 'reparos'] })
      queryClient.invalidateQueries({ queryKey: KEY })
    },
  })
}

export function useComentarios(dispositivoId: string | undefined) {
  return useQuery({
    queryKey: [...KEY, dispositivoId, 'comentarios'],
    queryFn: () => dispositivosApi.listarComentarios(dispositivoId!),
    enabled: !!dispositivoId,
  })
}

export function useCriarComentario(dispositivoId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: CriarComentarioRequest) => dispositivosApi.criarComentario(dispositivoId, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...KEY, dispositivoId, 'comentarios'] }),
  })
}

export function useComandos(dispositivoId: string | undefined) {
  return useQuery({
    queryKey: [...KEY, dispositivoId, 'comandos'],
    queryFn: () => dispositivosApi.listarComandos(dispositivoId!),
    enabled: !!dispositivoId,
  })
}

export function useEmitirComando(dispositivoId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (tipo: TipoComandoRemoto) => dispositivosApi.emitirComando(dispositivoId, tipo),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...KEY, dispositivoId, 'comandos'] })
      queryClient.invalidateQueries({ queryKey: KEY })
    },
  })
}

export function useGerarEnrollment(dispositivoId: string) {
  return useMutation({ mutationFn: () => dispositivosApi.gerarEnrollment(dispositivoId) })
}

export function useHistoricoLocalizacao(dispositivoId: string | undefined) {
  return useQuery({
    queryKey: [...KEY, dispositivoId, 'localizacao'],
    queryFn: () => dispositivosApi.historicoLocalizacao(dispositivoId!),
    enabled: !!dispositivoId,
  })
}

export function useAlertasDispositivo(dispositivoId: string | undefined) {
  return useQuery({
    queryKey: [...KEY, dispositivoId, 'alertas'],
    queryFn: () => dispositivosApi.listarAlertas(dispositivoId!),
    enabled: !!dispositivoId,
  })
}

export function useReconhecerAlerta(dispositivoId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (alertaId: string) => dispositivosApi.reconhecerAlerta(dispositivoId, alertaId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...KEY, dispositivoId, 'alertas'] }),
  })
}
