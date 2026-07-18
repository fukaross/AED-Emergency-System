import type { RescueMessage } from '@/types/rescue-session'
import { MESSAGE_TEXT_MAX_LENGTH } from '@/utils/rescue-session-constants'

export function getMessageScrollAnchor(messages: readonly RescueMessage[]): string {
  const latestSeq = messages.at(-1)?.seq ?? 0
  return `msg-bottom-${messages.length}-${latestSeq}`
}

export function prepareMessageDraft(draft: string): string | null {
  const text = draft.trim()
  if (!text || text.length > MESSAGE_TEXT_MAX_LENGTH) return null
  return text
}
