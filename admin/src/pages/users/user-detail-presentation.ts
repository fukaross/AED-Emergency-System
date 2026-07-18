import type { PrimaryRole, RescuerLevel, SchoolProfile } from '@/types/user'

const ROLE_LABELS: Readonly<Record<PrimaryRole, string>> = {
  student: '学生',
  teacher: '教师',
  staff: '职工',
  admin: '管理员',
}

const LEVEL_LABELS: Readonly<Record<RescuerLevel, string>> = {
  honor: '荣誉',
  trainee: '见习',
  official: '正式',
  core: '核心',
}

const BINDING_LABELS: Readonly<Record<SchoolProfile['bindingStatus'], string>> = {
  pending: '待审核',
  active: '已绑定',
  rejected: '已拒绝',
  replaced: '已替换',
  unbound: '已解绑',
}

export function getRoleCn(role: PrimaryRole) {
  return ROLE_LABELS[role]
}

export function getLevelCn(level: RescuerLevel | null) {
  return level ? LEVEL_LABELS[level] : '—'
}

export function getBindingStatusCn(status: SchoolProfile['bindingStatus']) {
  return BINDING_LABELS[status]
}

export function getBindingTagType(status: SchoolProfile['bindingStatus']) {
  if (status === 'active') return 'success'
  if (status === 'pending') return 'warning'
  if (status === 'rejected' || status === 'unbound') return 'danger'
  return 'info'
}

export function formatTime(dateStr: string | null) {
  if (!dateStr) return '—'
  const date = new Date(dateStr)
  if (Number.isNaN(date.getTime())) return dateStr
  const yyyy = date.getFullYear()
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const dd = String(date.getDate()).padStart(2, '0')
  const hh = String(date.getHours()).padStart(2, '0')
  const min = String(date.getMinutes()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd} ${hh}:${min}`
}

export function formatDateOnly(dateStr: string | null | undefined) {
  if (!dateStr) return '—'
  const date = new Date(dateStr)
  if (Number.isNaN(date.getTime())) return dateStr
  const yyyy = date.getFullYear()
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const dd = String(date.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

export function getInitials(name: string) {
  return name ? name.charAt(0) : 'U'
}

export function truncateOpenId(id: string) {
  if (!id || id.length <= 12) return id
  return `${id.slice(0, 6)}...${id.slice(-6)}`
}

export function maskPhone(phone: string | null) {
  return phone ? phone.replace(/(\d{3})\d{4}(\d{4})/, '$1****$2') : '—'
}

export function maskCertNo(certNo: string | null | undefined) {
  if (!certNo) return '—'
  return certNo.length <= 8 ? certNo : `${certNo.slice(0, 8)}****`
}
