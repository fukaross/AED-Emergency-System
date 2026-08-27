// 日期、距离、脱敏等格式化工具

/** 手机号脱敏：中间 4 位替换为 **** */
export function maskPhone(phone: string): string {
  if (!phone || phone.length < 7) return phone
  return phone.replace(/(\d{3})\d{4}(\d+)/, '$1****$2')
}

/** 身份证号脱敏：保留前 6 位和后 4 位 */
export function maskIdNumber(idNumber: string): string {
  if (!idNumber || idNumber.length < 10) return idNumber
  return idNumber.replace(/(.{6}).+(.{4})$/, '$1******$2')
}

/** 格式化距离：< 1000m 显示整数米，≥ 1000m 显示 km */
export function formatDistance(meters: number): string {
  if (meters >= 1000) {
    return `${(meters / 1000).toFixed(1)}km`
  }
  return `${Math.round(meters)}m`
}

/** 格式化日期为 YYYY-MM-DD */
export function formatDate(isoString: string): string {
  if (!isoString) return ''
  return isoString.slice(0, 10)
}

/** 格式化日期为 YYYY-MM-DD HH:mm */
export function formatDateTime(isoString: string): string {
  if (!isoString) return ''
  return isoString.slice(0, 16).replace('T', ' ')
}

/** 格式化为中文本地时间：YYYY年MM月DD日 HH:mm */
export function formatChineseDateTime(value: string | Date): string {
  if (!value) return ''

  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) return ''

  const year = date.getFullYear()
  const month = padDateUnit(date.getMonth() + 1)
  const day = padDateUnit(date.getDate())
  const hour = padDateUnit(date.getHours())
  const minute = padDateUnit(date.getMinutes())

  return `${year}年${month}月${day}日 ${hour}:${minute}`
}

/** 格式化日期为友好相对时间 */
export function formatRelativeTime(isoString: string): string {
  if (!isoString) return ''
  const date = new Date(isoString)
  const now = Date.now()
  const diffMs = now - date.getTime()
  const diffMin = Math.floor(diffMs / 60000)

  if (diffMin < 1) return '刚刚'
  if (diffMin < 60) return `${diffMin}分钟前`

  const diffHour = Math.floor(diffMin / 60)
  if (diffHour < 24) return `${diffHour}小时前`

  const diffDay = Math.floor(diffHour / 24)
  if (diffDay < 30) return `${diffDay}天前`

  return formatDate(isoString)
}

/** 会话消息/状态事件时间：同天显示 HH:mm:ss，跨天带日期前缀，精确到秒。 */
export function formatMessageTime(
  value: string | Date,
  now: Date = new Date(),
): string {
  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) return ''

  const time = `${padDateUnit(date.getHours())}:${padDateUnit(date.getMinutes())}:${padDateUnit(date.getSeconds())}`
  const startOfDay = (target: Date) =>
    new Date(target.getFullYear(), target.getMonth(), target.getDate()).getTime()
  const dayDiff = Math.round((startOfDay(now) - startOfDay(date)) / 86_400_000)

  if (dayDiff === 0) return time
  if (dayDiff === 1) return `昨天 ${time}`

  const monthDay = `${padDateUnit(date.getMonth() + 1)}-${padDateUnit(date.getDate())}`
  if (date.getFullYear() === now.getFullYear()) {
    return `${monthDay} ${time}`
  }
  return `${date.getFullYear()}-${monthDay} ${time}`
}

function padDateUnit(value: number): string {
  return String(value).padStart(2, '0')
}
