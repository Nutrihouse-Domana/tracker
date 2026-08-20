/** Utilitários de formatação no padrão brasileiro (CPF, telefone e data/hora). */

/** Formata uma data/hora ISO como DD/MM/AAAA HH:mm (padrão pt-BR). Vazio se nula/ inválida. */
export function formatarDataHora(valor: string | null | undefined) {
  if (!valor) return ''
  const data = new Date(valor)
  if (Number.isNaN(data.getTime())) return ''
  return data.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/** Formata só a data como DD/MM/AAAA (padrão pt-BR). */
export function formatarData(valor: string | null | undefined) {
  if (!valor) return ''
  const data = new Date(valor)
  if (Number.isNaN(data.getTime())) return ''
  return data.toLocaleDateString('pt-BR')
}

/** Mantém apenas os dígitos do texto. */
export function apenasDigitos(valor: string) {
  return valor.replace(/\D/g, '')
}

/** Formata CPF como 000.000.000-00, aplicando a máscara parcialmente enquanto se digita. */
export function formatarCpf(valor: string) {
  const d = apenasDigitos(valor).slice(0, 11)
  if (d.length <= 3) return d
  if (d.length <= 6) return `${d.slice(0, 3)}.${d.slice(3)}`
  if (d.length <= 9) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6)}`
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`
}

/**
 * Formata telefone como (00) 00000-0000 (celular) ou (00) 0000-0000 (fixo),
 * aplicando a máscara parcialmente enquanto se digita.
 */
export function formatarTelefone(valor: string) {
  const d = apenasDigitos(valor).slice(0, 11)
  if (d.length <= 2) return d.length ? `(${d}` : ''
  const ddd = `(${d.slice(0, 2)}) `
  const resto = d.slice(2)
  if (resto.length <= 4) return ddd + resto
  if (resto.length <= 8) return `${ddd}${resto.slice(0, 4)}-${resto.slice(4)}`
  return `${ddd}${resto.slice(0, 5)}-${resto.slice(5)}`
}
