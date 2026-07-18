export type DateTimeInput = string | number | Date | null | undefined

interface ParsedDateTime {
  date: Date | null
  raw: string
}

function parseDateTime(value: DateTimeInput): ParsedDateTime {
  if (value === null || value === undefined || value === '') return { date: null, raw: '' }
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? { date: null, raw: String(value) }
    : { date, raw: String(value) }
}

function twoDigits(value: number): string {
  return String(value).padStart(2, '0')
}

/** 管理端紧凑时间格式；无效服务端值保留原文，避免伪造时间。 */
export function formatCompactDateTime(value: DateTimeInput): string {
  const parsed = parseDateTime(value)
  if (parsed.date === null) return parsed.raw
  const month = twoDigits(parsed.date.getMonth() + 1)
  const day = twoDigits(parsed.date.getDate())
  const hour = twoDigits(parsed.date.getHours())
  const minute = twoDigits(parsed.date.getMinutes())
  return `${month}-${day} ${hour}:${minute}`
}

/** 管理端完整时间格式；无效服务端值保留原文。 */
export function formatDateTime(value: DateTimeInput): string {
  const parsed = parseDateTime(value)
  if (parsed.date === null) return parsed.raw
  const year = parsed.date.getFullYear()
  const month = twoDigits(parsed.date.getMonth() + 1)
  const day = twoDigits(parsed.date.getDate())
  const time = formatTimeOfDay(parsed.date, true)
  return `${year}-${month}-${day} ${time}`
}

/** 管理端时分或时分秒格式；无效服务端值保留原文。 */
export function formatTimeOfDay(value: DateTimeInput, includeSeconds = false): string {
  const parsed = parseDateTime(value)
  if (parsed.date === null) return parsed.raw
  const hour = twoDigits(parsed.date.getHours())
  const minute = twoDigits(parsed.date.getMinutes())
  if (!includeSeconds) return `${hour}:${minute}`
  return `${hour}:${minute}:${twoDigits(parsed.date.getSeconds())}`
}
