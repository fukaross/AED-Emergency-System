export function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  const idx = dateStr.indexOf('T')
  return idx > 0 ? dateStr.slice(0, idx) : dateStr.slice(0, 10)
}

export function formatViews(n?: number): string {
  if (!n || n < 0) return '0'
  if (n >= 10000) return `${(n / 10000).toFixed(1)}万`
  if (n >= 1000) return `${(n / 1000).toFixed(1)}k`
  return String(n)
}
