// 急救会话监控纯逻辑 —— 信号状态、消息文案、marker 元信息（可单测）
import type {
  AdminRescueMessage,
  RescueSignal,
} from '@/types/rescue-session'

export const RESCUE_SIGNAL_LOST_AFTER_SECONDS = 45
export const RESCUE_SIGNAL_GONE_AFTER_SECONDS = 90

const MS_PER_SECOND = 1000

const EVENT_LABELS = {
  joined: '加入了会话',
  taken_aed: '已拿取 AED',
  arrived: '已到达现场',
} as const

/** 与后端同一 TTL 规则：≤45s ok，≤90s lost，>90s gone（BackEnd.md §4.5A） */
export function computeSignal(
  lastSeenAt: string,
  now: number,
  lostAfterSeconds = RESCUE_SIGNAL_LOST_AFTER_SECONDS,
  goneAfterSeconds = RESCUE_SIGNAL_GONE_AFTER_SECONDS,
): RescueSignal {
  const elapsedSeconds = (now - Date.parse(lastSeenAt)) / MS_PER_SECOND
  if (elapsedSeconds <= lostAfterSeconds) return 'ok'
  if (elapsedSeconds <= goneAfterSeconds) return 'lost'
  return 'gone'
}

/** 消息 → 监控消息流文案（文字消息原文，状态事件结构化文案） */
export function formatRescueMessageText(message: AdminRescueMessage): string {
  if (message.kind === 'text') {
    return message.text ?? ''
  }
  const label = EVENT_LABELS[message.eventType ?? 'joined']
  if (message.action === 'revoke') {
    return `${message.senderName} 撤销了「${label}」`
  }
  return `${message.senderName} ${label}`
}

export interface RescueMarkerMeta {
  readonly color: string
  readonly label: string
}

/** 参与者 marker 元信息：拿 AED 绿色高亮，普通参与者蓝色，SOS 红色 */
export function participantMarkerMeta(participant: { takenAed: boolean }): RescueMarkerMeta {
  return participant.takenAed
    ? { color: '#4caf50', label: 'A' }
    : { color: '#2196f3', label: '急' }
}

export const SOS_MARKER_META: RescueMarkerMeta = { color: '#e53935', label: '救' }

/** 信号状态中文文案（监控列表/详情共用） */
export function signalText(signal: RescueSignal): string {
  if (signal === 'lost') return '信号丢失'
  if (signal === 'gone') return '已离线'
  return '在线'
}
