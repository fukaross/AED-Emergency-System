import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type {
  RescueMessage,
  RescueParticipant,
  RescueSessionSnapshot,
} from '@/types/rescue-session'

const apiMocks = vi.hoisted(() => ({
  joinRescueSession: vi.fn(),
  fetchRescueSession: vi.fn(),
  postRescueMessage: vi.fn(),
  postRescueImageMessage: vi.fn(),
  postRescueStatusEvent: vi.fn(),
  reportRescueLocation: vi.fn(),
  fetchRescueView: vi.fn(),
  fetchMineRescueSessions: vi.fn(),
}))

vi.mock('@/api/rescue-session', () => apiMocks)

import { useRescueSessionStore } from '@/store/rescue-session'

function buildMessage(seq: number, overrides: Partial<RescueMessage> = {}): RescueMessage {
  return {
    id: `msg-${seq}`,
    seq,
    kind: 'text',
    senderOpenId: 'openid-b',
    senderName: '李急救',
    text: `消息${seq}`,
    eventType: null,
    action: null,
    createdAt: '2026-07-17T10:00:00.000Z',
    ...overrides,
  }
}

function buildParticipant(openId: string): RescueParticipant {
  return {
    openId,
    displayName: openId,
    joinedAt: '2026-07-17T09:59:00.000Z',
    takenAed: false,
    arrived: false,
    signal: 'ok',
    location: null,
  }
}

function buildSnapshot(overrides: Partial<RescueSessionSnapshot> = {}): RescueSessionSnapshot {
  return {
    helpId: 'help-1',
    requestNo: 'HELP-1',
    helpStatus: 'responding',
    sos: { lat: 31.23, lng: 121.47, address: '测试点' },
    me: { openId: 'openid-a', takenAed: false, arrived: false, lastReadSeq: 0 },
    participants: [buildParticipant('openid-a')],
    messages: [buildMessage(1)],
    latestSeq: 1,
    serverTime: '2026-07-17T10:00:00.000Z',
    timing: {
      signalLostAfterSeconds: 45,
      locationGoneAfterSeconds: 90,
      locationReportIntervalSeconds: 5,
    },
    ...overrides,
  }
}

describe('rescue-session store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    Object.values(apiMocks).forEach((mock) => mock.mockReset())
  })

  it('joins a session and stores the snapshot', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    const store = useRescueSessionStore()

    await store.joinSession('help-1')

    expect(store.snapshot?.helpId).toBe('help-1')
    expect(store.sessionClosed).toBe(false)
  })

  it('merges incremental snapshots without duplicating messages', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    apiMocks.fetchRescueSession.mockResolvedValueOnce(
      buildSnapshot({
        messages: [buildMessage(1), buildMessage(2)],
        latestSeq: 2,
        participants: [buildParticipant('openid-a'), buildParticipant('openid-b')],
      }),
    )
    const store = useRescueSessionStore()
    await store.joinSession('help-1')

    await store.refreshSession('help-1')

    expect(store.snapshot?.messages).toHaveLength(2)
    expect(store.snapshot?.latestSeq).toBe(2)
    expect(store.snapshot?.participants).toHaveLength(2)
  })

  it('marks session closed when incremental help status closes', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    apiMocks.fetchRescueSession.mockResolvedValueOnce(
      buildSnapshot({ helpStatus: 'closed', messages: [], latestSeq: 1 }),
    )
    const store = useRescueSessionStore()
    await store.joinSession('help-1')

    await store.refreshSession('help-1')

    expect(store.sessionClosed).toBe(true)
    expect(store.snapshot?.helpStatus).toBe('closed')
  })

  it('sends a message and appends it once', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    apiMocks.postRescueMessage.mockResolvedValueOnce(buildMessage(2, { senderOpenId: 'openid-a' }))
    const store = useRescueSessionStore()
    await store.joinSession('help-1')

    await store.sendMessage('我到了')
    store.applySocketEnvelope('help-1', { type: 'message', data: buildMessage(2) })

    expect(store.snapshot?.messages).toHaveLength(2)
    expect(store.snapshot?.latestSeq).toBe(2)
  })

  it('updates my derived state after marking a status event', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    apiMocks.postRescueStatusEvent.mockResolvedValueOnce({
      appended: true,
      event: buildMessage(2, {
        kind: 'status_event',
        eventType: 'taken_aed',
        action: 'mark',
        senderOpenId: 'openid-a',
      }),
      me: { openId: 'openid-a', takenAed: true, arrived: false },
    })
    const store = useRescueSessionStore()
    await store.joinSession('help-1')

    await store.markStatus('taken_aed')

    expect(store.snapshot?.me.takenAed).toBe(true)
    expect(store.snapshot?.messages).toHaveLength(2)
  })

  it('treats message and participant envelopes only as catch-up hints', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    const store = useRescueSessionStore()
    await store.joinSession('help-1')

    const moved = { ...buildParticipant('openid-a'), takenAed: true }
    store.applySocketEnvelope('help-1', { type: 'participant', data: moved })
    store.applySocketEnvelope('help-1', { type: 'message', data: buildMessage(3) })
    store.applySocketEnvelope('help-1', { type: 'participant', data: buildParticipant('openid-c') })

    expect(store.snapshot?.participants).toHaveLength(1)
    expect(store.snapshot?.participants[0].takenAed).toBe(false)
    expect(store.snapshot?.messages.map((message) => message.seq)).toEqual([1])
    expect(store.snapshot?.latestSeq).toBe(1)
  })

  it('keeps the HTTP cursor behind an out-of-order websocket message', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    apiMocks.fetchRescueSession.mockResolvedValueOnce(
      buildSnapshot({
        messages: [buildMessage(2), buildMessage(3)],
        latestSeq: 3,
      }),
    )
    const store = useRescueSessionStore()
    await store.joinSession('help-1')

    store.applySocketEnvelope('help-1', { type: 'message', data: buildMessage(3) })
    await store.refreshSession('help-1')

    expect(apiMocks.fetchRescueSession).toHaveBeenCalledWith('help-1', {
      afterSeq: 1,
      silent: true,
    })
    expect(store.snapshot?.messages.map((message) => message.seq)).toEqual([1, 2, 3])
  })

  it('does not advance the confirmed cursor from a message POST response', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    apiMocks.postRescueMessage.mockResolvedValueOnce(buildMessage(3))
    apiMocks.fetchRescueSession.mockResolvedValueOnce(
      buildSnapshot({
        messages: [buildMessage(2), buildMessage(3)],
        latestSeq: 3,
      }),
    )
    const store = useRescueSessionStore()
    await store.joinSession('help-1')

    await store.sendMessage('第三条')
    await store.refreshSession('help-1')

    expect(apiMocks.fetchRescueSession).toHaveBeenCalledWith('help-1', {
      afterSeq: 1,
      silent: true,
    })
    expect(store.snapshot?.messages.map((message) => message.seq)).toEqual([1, 2, 3])
  })

  it('commits the final socket terminal frame immediately', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    const store = useRescueSessionStore()
    await store.joinSession('help-1')

    store.applySocketEnvelope('help-1', {
      type: 'help_status',
      data: { helpStatus: 'closed' },
    })

    expect(store.snapshot?.helpStatus).toBe('closed')
    expect(store.sessionClosed).toBe(true)
    expect(store.terminalHinted).toBe(true)
  })

  it('sums unread counts across mine items', async () => {
    apiMocks.fetchMineRescueSessions.mockResolvedValueOnce([
      { helpId: 'h1', unreadCount: 2 },
      { helpId: 'h2', unreadCount: 3 },
    ])
    const store = useRescueSessionStore()

    await store.loadMineSessions('openid-a')

    expect(store.totalUnread).toBe(5)
    expect(store.hasActiveSession).toBe(true)
    expect(store.mineLoaded).toBe(true)
  })

  it('resets session state', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    const store = useRescueSessionStore()
    await store.joinSession('help-1')

    store.resetSession()

    expect(store.snapshot).toBeNull()
    expect(store.sessionClosed).toBe(false)
  })
})
