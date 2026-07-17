import { describe, expect, it } from 'vitest'
import type { AdminRescueMessage } from '@/types/rescue-session'
import {
  computeSignal,
  formatRescueMessageText,
  participantMarkerMeta,
  signalText,
  SOS_MARKER_META,
} from './rescue-session'

function buildMessage(overrides: Partial<AdminRescueMessage>): AdminRescueMessage {
  return {
    id: 'msg-1',
    seq: 1,
    kind: 'status_event',
    senderOpenId: 'openid-1',
    senderName: '张急救',
    text: null,
    eventType: null,
    action: null,
    createdAt: '2026-07-17T10:00:00.000Z',
    ...overrides,
  }
}

describe('computeSignal', () => {
  const now = Date.parse('2026-07-17T10:00:00.000Z')
  const reportedAt = (secondsAgo: number) => new Date(now - secondsAgo * 1000).toISOString()

  it('uses location reported time for the inclusive 45s / 90s thresholds', () => {
    expect(computeSignal(reportedAt(45), now)).toBe('ok')
    expect(computeSignal(reportedAt(46), now)).toBe('lost')
    expect(computeSignal(reportedAt(90), now)).toBe('lost')
    expect(computeSignal(reportedAt(91), now)).toBe('gone')
  })
})

describe('formatRescueMessageText', () => {
  it('attributes text messages to their sender', () => {
    expect(formatRescueMessageText(buildMessage({ kind: 'text', text: '我到了' }))).toBe('张急救：我到了')
  })

  it('formats status events including revocations', () => {
    expect(formatRescueMessageText(buildMessage({ eventType: 'joined', action: 'mark' }))).toBe('张急救 加入了会话')
    expect(formatRescueMessageText(buildMessage({ eventType: 'taken_aed', action: 'mark' }))).toBe('张急救 已拿取 AED')
    expect(formatRescueMessageText(buildMessage({ eventType: 'arrived', action: 'revoke' }))).toBe('张急救 撤销了「已到达现场」')
  })
})

describe('participantMarkerMeta', () => {
  it('highlights AED carriers and greys out lost participants', () => {
    expect(participantMarkerMeta({ takenAed: true, signal: 'ok' }).color).toBe('#4caf50')
    expect(participantMarkerMeta({ takenAed: false, signal: 'ok' }).color).toBe('#2196f3')
    expect(participantMarkerMeta({ takenAed: true, signal: 'lost' }).color).toBe('#909399')
    expect(SOS_MARKER_META.color).toBe('#e53935')
  })
})

describe('signalText', () => {
  it('maps signals to Chinese labels', () => {
    expect(signalText('ok')).toBe('在线')
    expect(signalText('lost')).toBe('信号丢失')
    expect(signalText('gone')).toBe('已离线')
  })
})
