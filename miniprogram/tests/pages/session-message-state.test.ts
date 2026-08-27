import { describe, expect, it } from 'vitest'
import type { RescueMessage } from '@/types/rescue-session'
import {
  getMessageScrollAnchor,
  prepareMessageDraft,
  shouldShowLatestButton,
  shouldShowScrollTopButton,
} from '@/pages-sub/rescue/components/session-message-state'
import { MESSAGE_TEXT_MAX_LENGTH } from '@/utils/rescue-session-constants'

function message(seq: number): RescueMessage {
  return {
    id: `message-${seq}`,
    seq,
    kind: 'text',
    senderOpenId: 'rescuer-a',
    senderName: '急救员 A',
    text: `消息 ${seq}`,
    eventType: null,
    action: null,
    createdAt: '2026-07-18T10:00:00.000Z',
  }
}

describe('session message input and scrolling', () => {
  it('changes the bottom anchor for every consecutive message', () => {
    const first = getMessageScrollAnchor([message(1)])
    const second = getMessageScrollAnchor([message(1), message(2)])
    const third = getMessageScrollAnchor([message(1), message(2), message(3)])

    expect(new Set([first, second, third]).size).toBe(3)
    expect(third).toBe('msg-bottom-3-3')
  })

  it('accepts exactly 500 characters and rejects 501', () => {
    expect(MESSAGE_TEXT_MAX_LENGTH).toBe(500)
    expect(prepareMessageDraft('a'.repeat(500))).toHaveLength(500)
    expect(prepareMessageDraft('a'.repeat(501))).toBeNull()
  })

  it('trims a valid draft but preserves the caller-owned draft on rejection', () => {
    expect(prepareMessageDraft('  正在前往  ')).toBe('正在前往')
    expect(prepareMessageDraft('   ')).toBeNull()
  })

  it('shows top button only after scrolling past the threshold', () => {
    expect(shouldShowScrollTopButton(0, 80)).toBe(false)
    expect(shouldShowScrollTopButton(80, 80)).toBe(false)
    expect(shouldShowScrollTopButton(81, 80)).toBe(true)
  })

  it('shows latest button only when not near the bottom', () => {
    expect(shouldShowLatestButton(0, 1000, 400, 80)).toBe(true)
    expect(shouldShowLatestButton(520, 1000, 400, 80)).toBe(false)
    expect(shouldShowLatestButton(600, 1000, 400, 80)).toBe(false)
  })
})
