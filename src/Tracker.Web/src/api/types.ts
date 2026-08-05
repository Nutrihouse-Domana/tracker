export type TipoDispositivo = 'Notebook' | 'Telefone' | 'Periferico'
export type StatusDispositivo = 'EmEstoque' | 'EmUso' | 'EmManutencao' | 'Bloqueado' | 'Baixado'
export type TipoPeriferico = 'Mouse' | 'Teclado' | 'Monitor' | 'Dock' | 'Headset' | 'Outro'
export type StatusReparo = 'Aberto' | 'EmAndamento' | 'Concluido' | 'Cancelado'
export type TipoComandoRemoto = 'Bloquear' | 'Desbloquear'
export type StatusComandoRemoto = 'Pendente' | 'Enviado' | 'Confirmado' | 'Falhou'
export type OrigemLocalizacao = 'GpsAndroid' | 'IpGeolocationWindows' | 'WifiConhecida' | 'ManualTI'

export interface UsuarioAdm {
  id: string
  nome: string
  email: string
  roles: string[]
}

export interface Usuario {
  id: string
  nome: string
  cpf: string
  email: string
  telefone: string
  ativo: boolean
  quantidadeDispositivos: number
}

export interface CriarUsuarioRequest {
  nome: string
  cpf: string
  email: string
  telefone: string
}

export interface AtualizarUsuarioRequest {
  nome: string
  email: string
  telefone: string
  ativo: boolean
}

export interface Dispositivo {
  id: string
  tipo: TipoDispositivo
  numeroSerie: string
  /** Patrimônio. Nulo em periféricos, que não recebem etiqueta. */
  assetTag: string | null
  status: StatusDispositivo
  dataAquisicao: string
  observacoes: string | null
  usuarioAtualId: string | null
  usuarioAtualNome: string | null
  hostname: string | null
  sistemaOperacional: string | null
  versaoSO: string | null
  imei: string | null
  ultimoHeartbeatEm: string | null
  geofenceAtivo: boolean | null
  agenteVersao: string | null
  tipoPeriferico: TipoPeriferico | null
}

export interface CriarNotebookRequest {
  numeroSerie: string
  assetTag: string
  hostname: string
  sistemaOperacional: string
  versaoSO?: string | null
  macAddress?: string | null
  dataAquisicao: string
  observacoes?: string | null
}

export interface CriarTelefoneRequest {
  numeroSerie: string
  assetTag: string
  hostname: string
  sistemaOperacional: string
  imei: string
  macAddress?: string | null
  dataAquisicao: string
  observacoes?: string | null
}

export interface CriarPerifericoRequest {
  numeroSerie: string
  tipoPeriferico: TipoPeriferico
  dataAquisicao: string
  observacoes?: string | null
}

export interface AtribuirDispositivoRequest {
  usuarioId: string
  localDestinoId?: string | null
  motivo?: string | null
  registradoPorAdmId: string
}

export interface HistoricoMovimentacao {
  id: string
  dispositivoId: string
  usuarioOrigemId: string | null
  usuarioOrigemNome: string | null
  usuarioDestinoId: string | null
  usuarioDestinoNome: string | null
  localOrigemId: string | null
  localOrigemNome: string | null
  localDestinoId: string | null
  localDestinoNome: string | null
  dataMovimentacao: string
  motivo: string | null
}

export interface TermoDeUso {
  id: string
  dispositivoId: string
  usuarioId: string
  usuarioNome: string
  versaoTermo: string
  dataAceite: string
  hashDocumento: string
  caminhoArquivoAnexado: string
  assinadoAnexado: boolean
}

export interface EstatisticasDashboard {
  totalNotebooks: number
  totalTelefones: number
  totalPerifericos: number
  emUso: number
  emEstoque: number
  emManutencao: number
  bloqueados: number
  baixados: number
}

export interface HistoricoReparo {
  id: string
  dispositivoId: string
  descricao: string
  status: StatusReparo
  custoEstimado: number | null
  dataAbertura: string
  dataConclusao: string | null
  registradoPorAdmId: string
}

export interface CriarReparoRequest {
  descricao: string
  custoEstimado?: number | null
  registradoPorAdmId: string
}

export interface ComentarioDispositivo {
  id: string
  dispositivoId: string
  texto: string
  criadoPorAdmId: string
  criadoPorAdmNome: string
  criadoEm: string
}

export interface CriarComentarioRequest {
  texto: string
  criadoPorAdmId: string
}

export interface EnrollmentResponse {
  dispositivoId: string
  token: string
}

export interface ComandoRemoto {
  id: string
  dispositivoId: string
  tipoComando: TipoComandoRemoto
  status: StatusComandoRemoto
  emitidoPorAdmId: string
  dataEmissao: string
  dataConfirmacao: string | null
}

export interface HistoricoLocalizacao {
  id: string
  dispositivoId: string
  latitude: number
  longitude: number
  enderecoAproximado: string | null
  origem: OrigemLocalizacao
  precisaoMetros: number | null
  capturadoEm: string
}

export interface BuscaDispositivo {
  id: string
  tipo: TipoDispositivo
  status: StatusDispositivo
  numeroSerie: string
  assetTag: string | null
  hostname: string | null
  imei: string | null
  usuarioAtualNome: string | null
}

export interface BuscaUsuario {
  id: string
  nome: string
  email: string
  telefone: string
  ativo: boolean
  quantidadeDispositivos: number
}

export interface BuscaGlobal {
  termo: string
  dispositivos: BuscaDispositivo[]
  usuarios: BuscaUsuario[]
}
