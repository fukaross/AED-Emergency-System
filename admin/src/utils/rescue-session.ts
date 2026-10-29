// 急救会话监控纯逻辑 —— 信号状态、消息文案、marker 元信息（可单测）
import type {
  AdminRescueSessionDetail,
  RescueSignal,
} from '@/types/rescue-session'
import type { HelpRequestStatus } from '@/types/help'

const HELP_STATUSES: ReadonlySet<HelpRequestStatus> = new Set([
  'published', 'responding', 'pending_review', 'closed', 'false_alarm',
])
const RESCUE_SIGNALS: ReadonlySet<RescueSignal> = new Set(['ok', 'lost', 'gone'])

const EVENT_LABELS = {
  joined: '加入了会话',
  taken_aed: '已拿取 AED',
  arrived: '已到达现场',
  offline: '已离线',
  online: '已上线',
} as const

type MessageRecord = Readonly<Record<string, unknown>>

export class InvalidRescueSessionPayloadError extends Error {
  constructor(path: string, expected: string) {
    super(`急救会话详情数据无效：${path} 必须为${expected}`)
    this.name = 'InvalidRescueSessionPayloadError'
  }
}

/** 在外部详情进入响应式状态前验证所有渲染必需字段。 */
export function validateAdminRescueSessionDetail(value: unknown): AdminRescueSessionDetail {
  const detail = requireRecord(value, 'detail')
  requireString(detail.helpId, 'helpId')
  requireString(detail.requestNo, 'requestNo')
  requireEnum(detail.helpStatus, 'helpStatus', HELP_STATUSES)
  validateSos(detail.sos)
  validateRequester(detail.requester)
  requireNullableString(detail.closedAt, 'closedAt')
  requireNullableString(detail.closeNote, 'closeNote')
  requireArray(detail.participants, 'participants').forEach(validateParticipant)
  requireArray(detail.messages, 'messages')
  requireString(detail.serverTime, 'serverTime')
  return value as AdminRescueSessionDetail
}

function validateSos(value: unknown): void {
  const sos = requireRecord(value, 'sos')
  requireFiniteNumber(sos.lat, 'sos.lat')
  requireFiniteNumber(sos.lng, 'sos.lng')
  requireString(sos.address, 'sos.address')
}

function validateRequester(value: unknown): void {
  const requester = requireRecord(value, 'requester')
  requireNullableString(requester.openId, 'requester.openId')
  requireString(requester.displayName, 'requester.displayName')
}

function validateParticipant(value: unknown, index: number): void {
  const path = `participants[${index}]`
  const participant = requireRecord(value, path)
  requireString(participant.openId, `${path}.openId`)
  requireString(participant.displayName, `${path}.displayName`)
  requireString(participant.joinedAt, `${path}.joinedAt`)
  requireBoolean(participant.takenAed, `${path}.takenAed`)
  requireBoolean(participant.arrived, `${path}.arrived`)
  requireEnum(participant.signal, `${path}.signal`, RESCUE_SIGNALS)
  validateLocation(participant.location, path)
}

function validateLocation(value: unknown, participantPath: string): void {
  if (value === null) return
  const path = `${participantPath}.location`
  const location = requireRecord(value, path)
  requireFiniteNumber(location.lat, `${path}.lat`)
  requireFiniteNumber(location.lng, `${path}.lng`)
  requireString(location.reportedAt, `${path}.reportedAt`)
}

function requireRecord(value: unknown, path: string): Record<string, unknown> {
  if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
    return value as Record<string, unknown>
  }
  throw new InvalidRescueSessionPayloadError(path, '对象')
}

function requireArray(value: unknown, path: string): unknown[] {
  if (Array.isArray(value)) return value
  throw new InvalidRescueSessionPayloadError(path, '数组')
}

function requireString(value: unknown, path: string): asserts value is string {
  if (typeof value === 'string') return
  throw new InvalidRescueSessionPayloadError(path, '字符串')
}

function requireNullableString(value: unknown, path: string): void {
  if (value === null || typeof value === 'string') return
  throw new InvalidRescueSessionPayloadError(path, '字符串或 null')
}

function requireFiniteNumber(value: unknown, path: string): asserts value is number {
  if (typeof value === 'number' && Number.isFinite(value)) return
  throw new InvalidRescueSessionPayloadError(path, '有限数字')
}

function requireBoolean(value: unknown, path: string): asserts value is boolean {
  if (typeof value === 'boolean') return
  throw new InvalidRescueSessionPayloadError(path, '布尔值')
}

function requireEnum<T extends string>(
  value: unknown,
  path: string,
  allowed: ReadonlySet<T>,
): asserts value is T {
  if (typeof value === 'string' && allowed.has(value as T)) return
  throw new InvalidRescueSessionPayloadError(path, '受支持的枚举值')
}

export interface RescueMessageView {
  readonly key: string
  readonly isStatusEvent: boolean
  readonly isImage: boolean
  readonly imageUrl: string | null
  readonly senderOpenId: string
  readonly text: string
  readonly createdAt: string | null
}

/** 将不可信消息数组投影为不会中断 Vue 渲染的显式监控项。 */
export function buildRescueMessageViews(messages: unknown): readonly RescueMessageView[] {
  if (!Array.isArray(messages)) {
    return [invalidMessageView('消息列表不是数组')]
  }
  return messages.map(buildRescueMessageView)
}

/** 消息 → 监控消息流文案（非法外部数据也显示为明确契约错误）。 */
export function formatRescueMessageText(message: unknown): string {
  const record = asMessageRecord(message)
  if (!record) return invalidMessageText('', '消息条目不是对象')
  if (record.kind === 'text') return formatTextMessage(record)

  if (record.kind === 'image') {
    return typeof record.imageUrl === 'string' && record.imageUrl.trim()
      ? '图片消息'
      : invalidMessageText(messageId(record), '图片消息缺少图片地址')
  }

  if (record.kind !== 'status_event') {
    return invalidMessageText(messageId(record), '不支持的消息类型')
  }

  return formatStatusEvent(record)
}

function formatTextMessage(message: MessageRecord): string {
  if (typeof message.text !== 'string' || !message.text.trim()) {
    return invalidMessageText(messageId(message), '文字消息缺少正文')
  }
  if (typeof message.senderName !== 'string' || !message.senderName.trim()) {
    return invalidMessageText(messageId(message), '发送者名称无效')
  }
  return `${message.senderName}：${message.text}`
}

function formatStatusEvent(message: MessageRecord): string {
  const label = eventLabel(message.eventType)
  if (!label
    || typeof message.action !== 'string'
    || !['mark', 'revoke'].includes(message.action)) {
    return invalidMessageText(messageId(message), '状态事件字段无效')
  }
  if (message.eventType === 'joined' && message.action !== 'mark') {
    return invalidMessageText(messageId(message), '加入事件不可撤销')
  }
  if (typeof message.senderName !== 'string' || !message.senderName.trim()) {
    return invalidMessageText(messageId(message), '发送者名称无效')
  }
  if (message.action === 'revoke') {
    return `${message.senderName} 撤销了「${label}」`
  }
  return `${message.senderName} ${label}`
}

function eventLabel(eventType: unknown): string | null {
  if (typeof eventType !== 'string'
    || !Object.prototype.hasOwnProperty.call(EVENT_LABELS, eventType)) {
    return null
  }
  return EVENT_LABELS[eventType as keyof typeof EVENT_LABELS]
}

function invalidMessageText(messageId: string, reason: string): string {
  return `消息 ${messageId || '(unknown)'} 无法显示（契约无效：${reason}）`
}

function buildRescueMessageView(message: unknown, index: number): RescueMessageView {
  const record = asMessageRecord(message)
  const id = record ? messageId(record) : ''
  return {
    key: `${id || 'invalid'}:${index}`,
    isStatusEvent: record?.kind === 'status_event',
    isImage: record?.kind === 'image',
    imageUrl: record?.kind === 'image' && typeof record.imageUrl === 'string'
      ? record.imageUrl
      : null,
    senderOpenId: typeof record?.senderOpenId === 'string' ? record.senderOpenId : '',
    text: formatRescueMessageText(message),
    createdAt: typeof record?.createdAt === 'string' && record.createdAt.trim()
      ? record.createdAt
      : null,
  }
}

function invalidMessageView(reason: string): RescueMessageView {
  return {
    key: 'invalid-message-list',
    isStatusEvent: false,
    isImage: false,
    imageUrl: null,
    senderOpenId: '',
    text: invalidMessageText('', reason),
    createdAt: null,
  }
}

function asMessageRecord(message: unknown): MessageRecord | null {
  return message !== null && typeof message === 'object'
    ? message as MessageRecord
    : null
}

function messageId(message: MessageRecord): string {
  return typeof message.id === 'string' ? message.id : ''
}

export interface RescueMarkerMeta {
  readonly color: string
  readonly label: string
}

/** 参与者 marker 元信息：lost 灰显优先于拿 AED 高亮。 */
export function participantMarkerMeta(
  participant: { takenAed: boolean; signal: RescueSignal },
): RescueMarkerMeta {
  const label = participant.takenAed ? 'A' : '急'
  if (participant.signal !== 'ok') {
    return { color: '#909399', label }
  }
  return participant.takenAed
    ? { color: '#4caf50', label }
    : { color: '#2196f3', label }
}

export const SOS_MARKER_META: RescueMarkerMeta = { color: '#e53935', label: '救' }

/** 信号状态中文文案（监控列表/详情共用） */
export function signalText(signal: RescueSignal): string {
  if (signal === 'lost') return '信号丢失'
  if (signal === 'gone') return '已离线'
  return '在线'
}
