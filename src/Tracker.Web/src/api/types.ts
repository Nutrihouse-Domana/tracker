export type TipoDispositivo = 'Notebook' | 'Mobile' | 'Periferico'
export type StatusDispositivo = 'EmEstoque' | 'EmUso' | 'EmManutencao' | 'Bloqueado' | 'Baixado'
export type TipoPeriferico = 'Mouse' | 'Teclado' | 'Monitor' | 'Dock' | 'Headset' | 'Outro'
export type StatusReparo = 'Aberto' | 'EmAndamento' | 'Concluido' | 'Cancelado'
export type TipoComandoRemoto = 'Bloquear' | 'Desbloquear'
export type StatusComandoRemoto = 'Pendente' | 'Enviado' | 'Confirmado' | 'Falhou'
export type OrigemLocalizacao = 'GpsAndroid' | 'IpGeolocationWindows' | 'WifiConhecida' | 'ManualTI'

/** Quão protegido está o agente contra remoção pelo usuário. Só se aplica a Android. */
export type NivelProtecaoAgente = 'Nenhuma' | 'DeviceAdmin' | 'DeviceOwner'

export type TipoAlertaDispositivo =
  | 'AdminDesativado'
  | 'LocalizacaoNegada'
  | 'BloqueioReaplicado'
  | 'ProtecaoRebaixada'

export interface UsuarioAdm {
  id: string
  nome: string
  email: string
  roles: string[]
  /** Permissões do usuário logado (valores do catálogo). Gate da UI. */
  permissoes: string[]
}

// --- Acessos (logins da plataforma) e permissões ---

/** Um login gerenciável na tela de Acessos. */
export interface Acesso {
  id: string
  nome: string
  email: string
  ativo: boolean
  ultimoLoginEm: string | null
  permissoes: string[]
}

/** Metadados de uma permissão, para montar os checkboxes agrupados. */
export interface Permissao {
  valor: string
  rotulo: string
  descricao: string
  grupo: string
}

export interface CriarAcessoRequest {
  nome: string
  email: string
  cpf?: string | null
  senha: string
  permissoes: string[]
  /** Senha do ADM logado — reconferência da ação sensível. */
  senhaAdm: string
}

export interface AtualizarAcessoRequest {
  nome: string
  email: string
  cpf?: string | null
  permissoes: string[]
  senhaAdm: string
}

export interface AlterarAtivoAcessoRequest {
  ativo: boolean
  senhaAdm: string
}

export interface RedefinirSenhaAcessoRequest {
  novaSenha: string
  senhaAdm: string
}

// --- Auditoria ---

export interface LogAuditoria {
  id: string
  dataHora: string
  admId: string | null
  admNome: string
  acao: string
  entidadeTipo: string | null
  entidadeId: string | null
  descricao: string
  detalhes: string | null
  enderecoIp: string | null
}

export interface PaginaAuditoria {
  itens: LogAuditoria[]
  total: number
  pagina: number
  tamanhoPagina: number
}

export interface AcaoAuditoriaOpcao {
  valor: string
  rotulo: string
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

/** Unidade da Nutrihouse (matriz, filial) que o dispositivo atende. */
export interface Unidade {
  id: string
  nome: string
  codigo: string | null
  ativa: boolean
  quantidadeDispositivos: number
}

export interface CriarUnidadeRequest {
  nome: string
  codigo?: string | null
}

export interface AtualizarUnidadeRequest {
  nome: string
  codigo?: string | null
  ativa: boolean
}

export interface Dispositivo {
  id: string
  tipo: TipoDispositivo
  numeroSerie: string
  /** Patrimônio. Nulo em periféricos, que não recebem etiqueta. */
  assetTag: string | null
  /** Rótulo interno definido pela TI. Quando preenchido, é a identificação exibida do aparelho. */
  identificacaoInterna: string | null
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
  /** Telemetria do agente móvel; nula em notebooks Windows. */
  fabricante: string | null
  modelo: string | null
  bateriaPercentual: number | null
  nivelProtecao: NivelProtecaoAgente | null
  /** Unidade que o dispositivo atende. */
  unidadeId: string | null
  unidadeNome: string | null
  /** False = ativo do local: lotado na unidade, sem detentor individual. */
  pertenceAUsuario: boolean
  /** Quem responde pelo ativo: o usuário nominal ou, se for do local, a unidade. */
  responsavelNome: string | null
  /** Preenchido só em dispositivos arquivados. */
  excluidoEm: string | null
  motivoExclusao: string | null
  /** Vencimento do contrato de aluguel (ISO). Nulo nos ativos próprios, sem contrato registrado. */
  vencimentoAluguel: string | null
}

export interface ExcluirDispositivoRequest {
  senha: string
  motivo?: string | null
  /** false = arquiva (restaurável); true = apaga o registro e todo o histórico, sem volta. */
  definitiva?: boolean
}

export interface DefinirPosseDispositivoRequest {
  pertenceAUsuario: boolean
  unidadeId?: string | null
  senha: string
  motivo?: string | null
}

export interface DefinirVencimentoAluguelRequest {
  /** Data ISO do vencimento; null limpa o campo. Não exige senha do ADM. */
  vencimentoAluguel: string | null
  motivo?: string | null
}

export interface DefinirIdentificacaoInternaRequest {
  /** Vazio/null limpa o campo e volta a exibir o hostname/número de série. */
  identificacaoInterna: string | null
  senha: string
  motivo?: string | null
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
  unidadeId?: string | null
  pertenceAUsuario?: boolean
}

/** Celular ou tablet: mesmo tipo, mesmo agente. */
export interface CriarMobileRequest {
  numeroSerie: string
  assetTag: string
  hostname: string
  sistemaOperacional: string
  /** Opcional: tablet só-Wi-Fi não tem IMEI. */
  imei?: string | null
  macAddress?: string | null
  dataAquisicao: string
  observacoes?: string | null
  unidadeId?: string | null
  pertenceAUsuario?: boolean
}

export interface CriarPerifericoRequest {
  numeroSerie: string
  tipoPeriferico: TipoPeriferico
  dataAquisicao: string
  observacoes?: string | null
  unidadeId?: string | null
  pertenceAUsuario?: boolean
}

export interface AtribuirDispositivoRequest {
  usuarioId: string
  localDestinoId?: string | null
  motivo?: string | null
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
  registradoPorAdmNome: string | null
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
  totalMobiles: number
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
  registradoPorAdmNome: string | null
}

export interface CriarReparoRequest {
  descricao: string
  custoEstimado?: number | null
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
}

export interface EnrollmentResponse {
  dispositivoId: string
  token: string
  assetTag: string | null
}

export interface AlertaDispositivo {
  id: string
  dispositivoId: string
  tipo: TipoAlertaDispositivo
  detalhe: string | null
  reconhecido: boolean
  criadoEm: string
  reconhecidoEm: string | null
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
  unidadeNome: string | null
  responsavelNome: string | null
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

// --- Atualização OTA do agente Android ---

export interface PacoteAgenteAndroid {
  id: string
  versaoCode: number
  versaoNome: string
  nomeArquivoOriginal: string
  hashSha256: string
  tamanhoBytes: number
  criadoEm: string
}

export interface SituacaoAgenteAndroid {
  pacoteVigente: PacoteAgenteAndroid | null
  historico: PacoteAgenteAndroid[]
  dispositivosNaVersao: number
  dispositivosDesatualizados: number
}
