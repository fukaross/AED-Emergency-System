import { describe, expect, it } from 'vitest'
import { parseRescueSocketFrame } from '@/services/rescue-socket-frame'

const VALID_MESSAGE = {
  id: 'message-2',
  seq: 2,
  kind: 'text',
  senderOpenId: 'rescuer-a',
  senderName: '急救员 A',
  text: '正在前往',
  eventType: null,
  action: null,
  createdAt: '2026-07-18T10:00:00.000Z',
}

const VALID_PARTICIPANT = {
  openId: 'rescuer-a',
  displayName: '急救员 A',
  joinedAt: '2026-07-18T09:59:00.000Z',
  takenAed: true,
  arrived: false,
  signal: 'ok',
  location: {
    lat: 19.97,
    lng: 110.51,
    reportedAt: '2026-07-18T10:00:00.000Z',
  },
}

function frame(type: string, data: unknown): string {
  return JSON.stringify({ type, data })
}

describe('parseRescueSocketFrame', () => {
  it.each([
    ['message', VALID_MESSAGE],
    ['participant', VALID_PARTICIPANT],
    ['help_status', { helpStatus: 'closed' }],
  ])('accepts a fully valid %s envelope', (type, data) => {
    const result = parseRescueSocketFrame(frame(type, data))

    expect(result).toMatchObject({ ok: true, envelope: { type, data } })
  })

  it.each([
    ['', 'unsupported_data'],
    ['not-json', 'malformed_json'],
    [JSON.stringify({ type: 'message' }), 'invalid_envelope'],
    [frame('unknown', {}), 'invalid_envelope'],
  ])('rejects untrusted frame %j as %s', (raw, reason) => {
    expect(parseRescueSocketFrame(raw)).toEqual({ ok: false, reason })
  })

  it('rejects binary frames explicitly', () => {
    expect(parseRescueSocketFrame(new ArrayBuffer(8))).toEqual({
      ok: false,
      reason: 'unsupported_data',
    })
  })

  it.each([
    { ...VALID_MESSAGE, seq: 0 },
    { ...VALID_MESSAGE, seq: 1.5 },
    { ...VALID_MESSAGE, createdAt: 'not-a-time' },
    { ...VALID_MESSAGE, text: null },
    { ...VALID_MESSAGE, eventType: 'arrived' },
  ])('rejects an invalid message payload', (message) => {
    expect(parseRescueSocketFrame(frame('message', message))).toMatchObject({
      ok: false,
      reason: 'invalid_envelope',
    })
  })

  it('rejects non-finite coordinates produced by JSON number overflow', () => {
    const raw = frame('participant', VALID_PARTICIPANT)
      .replace('19.97', '1e999')

    expect(parseRescueSocketFrame(raw)).toMatchObject({ ok: false })
  })

  it('rejects an impossible joined revoke event', () => {
    const event = {
      ...VALID_MESSAGE,
      kind: 'status_event',
      text: null,
      eventType: 'joined',
      action: 'revoke',
    }

    expect(parseRescueSocketFrame(frame('message', event))).toMatchObject({ ok: false })
  })
})
