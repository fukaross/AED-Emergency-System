import type { RescueMessage } from '@/types/rescue-session'
import { MESSAGE_TEXT_MAX_LENGTH } from '@/utils/rescue-session-constants'

export function getMessageScrollAnchor(messages: readonly RescueMessage[]): string {
  const latestSeq = messages.at(-1)?.seq ?? 0
  return `msg-bottom-${messages.length}-${latestSeq}`
}

/** 距顶部超过阈值时显示「回顶部」。 */
export function shouldShowScrollTopButton(
  scrollTop: number,
  thresholdPx: number,
): boolean {
  return scrollTop > thresholdPx
}

/** 距底部超过阈值时显示「最新」。 */
export function shouldShowLatestButton(
  scrollTop: number,
  scrollHeight: number,
  clientHeight: number,
  thresholdPx: number,
): boolean {
  return scrollHeight - clientHeight - scrollTop > thresholdPx
}

export function prepareMessageDraft(draft: string): string | null {
  const text = draft.trim()
  if (!text || text.length > MESSAGE_TEXT_MAX_LENGTH) return null
  return text
}
