import { isAxiosError } from 'axios'

/**
 * Extrai a mensagem que a API devolveu ({ message: ... } dos controllers, ou os erros de
 * validação do ValidationProblemDetails). Importa mostrar o texto do servidor em vez de um
 * genérico: é ele que diz "Senha incorreta" ou por que a operação foi barrada.
 */
export function mensagemErroApi(erro: unknown, fallback: string) {
  if (!isAxiosError(erro)) return fallback

  const data = erro.response?.data as
    | { message?: string; errors?: Record<string, string[]>; title?: string }
    | undefined

  if (data?.message) return data.message

  const primeiroErroDeValidacao = data?.errors ? Object.values(data.errors).flat()[0] : undefined
  return primeiroErroDeValidacao ?? data?.title ?? fallback
}
