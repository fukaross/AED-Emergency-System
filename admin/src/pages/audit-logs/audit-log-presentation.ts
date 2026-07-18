type EventTypeStyle = Readonly<Record<'background' | 'border' | 'color', string>>

const TIME_PART_WIDTH = 2
const IPV6_MIN_PARTS = 3

const EVENT_NAME_LABELS: Readonly<Record<string, string>> = {
  'admin.bootstrap_created': '系统初始化',
  'admin.login': '管理员登录',
  'admin.logout': '管理员登出',
  'admin.session_revoked': '会话撤销',
  'admin.super_admin_reset': '超管重置',
  'admin.password_reset': '管理员密码重置',
  'admin.admin_created': '创建管理员',
  'admin.admin_deleted': '删除管理员',
  'admin.created': '创建管理员',
  'admin.deleted': '删除管理员',
  'help.create': '发布求助',
  'help.report_submitted': '提交报告',
  'help.close': '关闭求助',
  'help.false_alarm': '标记误报',
  'user.updated': '用户更新',
}

const EVENT_TYPE_LABELS: Readonly<Record<string, string>> = {
  admin: '管理',
  help: '求助',
  user: '用户',
}

const EVENT_TYPE_STYLES: Readonly<Record<string, EventTypeStyle>> = {
  admin: { background: '#ecf5ff', color: '#409eff', border: '1px solid #ecf5ff' },
  help: { background: '#fdf6ec', color: '#e6a23c', border: '1px solid #fdf6ec' },
  user: { background: '#f0f9eb', color: '#67c23a', border: '1px solid #f0f9eb' },
}

const DEFAULT_EVENT_TYPE_STYLE: EventTypeStyle = {
  background: '#f4f4f5',
  color: '#909399',
  border: '1px solid #f4f4f5',
}

function twoDigits(value: number): string {
  return String(value).padStart(TIME_PART_WIDTH, '0')
}

export function getEventNameCn(name: string): string {
  return EVENT_NAME_LABELS[name] ?? name
}

export function getEventTypeLabel(type: string): string {
  return EVENT_TYPE_LABELS[type] ?? type
}

export function getEventTypeStyle(type: string): EventTypeStyle {
  return EVENT_TYPE_STYLES[type] ?? DEFAULT_EVENT_TYPE_STYLE
}

/** 审计列表保留秒级精度；无效服务端值原样展示，避免伪造时间。 */
export function formatTimeSeconds(dateStr: string | null): string {
  if (!dateStr) return '—'
  const date = new Date(dateStr)
  if (Number.isNaN(date.getTime())) return dateStr
  const datePart = `${twoDigits(date.getMonth() + 1)}-${twoDigits(date.getDate())}`
  const timePart = [date.getHours(), date.getMinutes(), date.getSeconds()]
    .map(twoDigits)
    .join(':')
  return `${datePart} ${timePart}`
}

/** 仅降低审计展示中的 IP 精度，不改变存储的原始审计数据。 */
export function maskIp(ip: string | null): string {
  if (!ip) return '—'
  const v4Match = ip.match(/^(\d+)\.(\d+)\.(\d+)\.(\d+)$/)
  if (v4Match) return `${v4Match[1]}.${v4Match[2]}.*.*`
  const parts = ip.split(':')
  if (parts.length >= IPV6_MIN_PARTS) return `${parts[0]}:${parts[1]}:*:*`
  return ip
}

/** 展开区单独展示 changedFields，因此 JSON 副本中移除该重复字段。 */
export function formatJson(metadata: Record<string, unknown> | null): string {
  if (!metadata) return '无附加信息'
  const displayMetadata = { ...metadata }
  delete displayMetadata.changedFields
  if (Object.keys(displayMetadata).length === 0) return '无额外属性'
  return JSON.stringify(displayMetadata, null, TIME_PART_WIDTH)
}
