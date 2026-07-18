import { describe, expect, it } from 'vitest'
import type {
  AdminRescueMessage,
  AdminRescueStatusEventMessage,
  AdminRescueTextMessage,
} from '@/types/rescue-session'
import {
  buildRescueMessageViews,
  formatRescueMessageText,
  participantMarkerMeta,
  signalText,
  SOS_MARKER_META,
} from './rescue-session'

const messageBase = {
  id: 'msg-1',
  seq: 1,
  senderOpenId: 'openid-1',
  senderName: '张急救',
  createdAt: '2026-07-17T10:00:00.000Z',
} as const

function textMessage(text = '我到了'): AdminRescueTextMessage {
  return { ...messageBase, kind: 'text', text, eventType: null, action: null }
}

function statusMessage(
  overrides: Partial<AdminRescueStatusEventMessage> = {},
): AdminRescueStatusEventMessage {
  return {
    ...messageBase,
    kind: 'status_event',
    text: null,
    eventType: 'joined',
    action: 'mark',
    ...overrides,
  }
}

describe('formatRescueMessageText', () => {
  it('attributes text messages to their sender', () => {
    expect(formatRescueMessageText(textMessage())).toBe('张急救：我到了')
  })

  it('formats status events including revocations', () => {
    expect(formatRescueMessageText(statusMessage())).toBe('张急救 加入了会话')
    expect(formatRescueMessageText(statusMessage({ eventType: 'taken_aed' }))).toBe('张急救 已拿取 AED')
    expect(formatRescueMessageText(statusMessage({ eventType: 'arrived', action: 'revoke' })))
      .toBe('张急救 撤销了「已到达现场」')
  })

  it('renders invalid external messages as visible warnings without crashing the detail', () => {
    const missingEvent = { ...statusMessage(), eventType: null } as unknown as AdminRescueMessage
    const revokedJoin = statusMessage({ action: 'revoke' })
    const emptyText = textMessage('')
    const unknownKind = { ...messageBase, kind: 'future_kind' } as unknown as AdminRescueMessage

    expect(formatRescueMessageText(missingEvent)).toContain('状态事件字段无效')
    expect(formatRescueMessageText(revokedJoin)).toContain('加入事件不可撤销')
    expect(formatRescueMessageText(emptyText)).toContain('文字消息缺少正文')
    expect(formatRescueMessageText(unknownKind)).toContain('不支持的消息类型')
    expect(formatRescueMessageText(statusMessage({ eventType: 'constructor' as never })))
      .toContain('状态事件字段无效')
  })

  it('projects null entries and malformed collections into visible warnings', () => {
    const views = buildRescueMessageViews([null, undefined, textMessage()])

    expect(views).toHaveLength(3)
    expect(views[0]?.text).toContain('消息条目不是对象')
    expect(views[0]?.createdAt).toBeNull()
    expect(views[1]?.text).toContain('消息条目不是对象')
    expect(views[2]?.text).toBe('张急救：我到了')
    expect(buildRescueMessageViews(null)[0]?.text).toContain('消息列表不是数组')
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
  it('maps server-owned signals to Chinese labels', () => {
    expect(signalText('ok')).toBe('在线')
    expect(signalText('lost')).toBe('信号丢失')
    expect(signalText('gone')).toBe('已离线')
  })
})
