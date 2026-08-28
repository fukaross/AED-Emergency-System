import { describe, expect, it } from 'vitest'
import type { RescueMessage, RescueViewEvent } from '@/types/rescue-session'
import {
  formatRescueMessageText,
  formatRescueViewEventText,
} from '@/utils/rescue-format'

function buildMessage(overrides: Partial<RescueMessage>): RescueMessage {
  return {
    id: 'msg-1',
    seq: 1,
    kind: 'status_event',
    senderOpenId: 'openid-1',
    senderName: '张急救',
    text: null,
    imageUrl: null,
    eventType: null,
    action: null,
    createdAt: '2026-07-17T10:00:00.000Z',
    ...overrides,
  }
}

describe('rescue-format', () => {
  it('returns raw text for text messages', () => {
    expect(formatRescueMessageText(buildMessage({ kind: 'text', text: '我到了' }))).toBe('我到了')
  })

  it('formats joined / taken_aed / arrived events', () => {
    expect(formatRescueMessageText(buildMessage({ eventType: 'joined', action: 'mark' }))).toBe('张急救 加入了会话，正在赶来')
    expect(formatRescueMessageText(buildMessage({ eventType: 'taken_aed', action: 'mark' }))).toBe('张急救 已拿取 AED')
    expect(formatRescueMessageText(buildMessage({ eventType: 'arrived', action: 'mark' }))).toBe('张急救 已到达现场')
  })

  it('formats offline / online events', () => {
    expect(formatRescueMessageText(buildMessage({ eventType: 'offline', action: 'mark' }))).toBe('张急救 已离线')
    expect(formatRescueMessageText(buildMessage({ eventType: 'online', action: 'mark' }))).toBe('张急救 已上线')
  })

  it('formats revoke events as revocations', () => {
    expect(formatRescueMessageText(buildMessage({ eventType: 'taken_aed', action: 'revoke' }))).toBe('张急救 撤销了「已拿取 AED」')
    expect(formatRescueMessageText(buildMessage({ eventType: 'arrived', action: 'revoke' }))).toBe('张急救 撤销了「已到达现场」')
  })

  it('formats rescue view events with actor name', () => {
    const event: RescueViewEvent = {
      seq: 3,
      eventType: 'taken_aed',
      action: 'mark',
      actorName: '李急救',
      createdAt: '2026-07-17T10:01:00.000Z',
    }
    expect(formatRescueViewEventText(event)).toBe('李急救 已拿取 AED')
    expect(formatRescueViewEventText({ ...event, action: 'revoke' })).toBe('李急救 撤销了「已拿取 AED」')
  })
})
