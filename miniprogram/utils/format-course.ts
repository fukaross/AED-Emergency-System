export function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  const idx = dateStr.indexOf('T')
  return idx > 0 ? dateStr.slice(0, idx) : dateStr.slice(0, 10)
}

/** 阅读量纯数字展示（如 19000），不做 万/k 缩写 */
export function formatViews(n?: number): string {
  if (!n || n < 0) return '0'
  return String(n)
}
