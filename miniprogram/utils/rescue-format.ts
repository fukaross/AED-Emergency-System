// 会话消息展示文案 —— 纯函数（状态事件系统消息）
import type { RescueMessage, RescueViewEvent } from '@/types/rescue-session'

const EVENT_LABELS = {
  joined: '加入了会话，正在赶来',
  taken_aed: '已拿取 AED',
  arrived: '已到达现场',
} as const

/** 状态事件 → 消息流系统文案；文字消息返回原文 */
export function formatRescueMessageText(message: RescueMessage): string {
  if (message.kind === 'text') {
    return message.text ?? ''
  }
  const label = EVENT_LABELS[message.eventType ?? 'joined']
  if (message.action === 'revoke') {
    return `${message.senderName} 撤销了「${label}」`
  }
  return `${message.senderName} ${label}`
}

/** 求助人只读视图的状态事件 → 时间线文案 */
export function formatRescueViewEventText(event: RescueViewEvent): string {
  const label = EVENT_LABELS[event.eventType]
  if (event.action === 'revoke') {
    return `${event.actorName} 撤销了「${label}」`
  }
  return `${event.actorName} ${label}`
}
