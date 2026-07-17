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
    sos: { lat: 19.97, lng: 110.51, address: '测试点' },
    me: { openId: 'openid-a', takenAed: false, arrived: false, lastReadSeq: 0 },
    participants: [buildParticipant('openid-a')],
    messages: [buildMessage(1)],
    latestSeq: 1,
    serverTime: '2026-07-17T10:00:00.000Z',
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
    await store.applySocketEnvelope({ type: 'message', data: buildMessage(2) })

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

  it('applies participant envelopes by replacing the matching member', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    const store = useRescueSessionStore()
    await store.joinSession('help-1')

    const moved = { ...buildParticipant('openid-a'), takenAed: true }
    store.applySocketEnvelope({ type: 'participant', data: moved })

    expect(store.snapshot?.participants[0].takenAed).toBe(true)

    store.applySocketEnvelope({ type: 'participant', data: buildParticipant('openid-c') })
    expect(store.snapshot?.participants).toHaveLength(2)
  })

  it('applies help_status envelopes and closes the session', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot())
    const store = useRescueSessionStore()
    await store.joinSession('help-1')

    store.applySocketEnvelope({ type: 'help_status', data: { helpStatus: 'closed' } })

    expect(store.snapshot?.helpStatus).toBe('closed')
    expect(store.sessionClosed).toBe(true)
  })

  it('sums unread counts across mine items', async () => {
    apiMocks.fetchMineRescueSessions.mockResolvedValueOnce([
      { helpId: 'h1', unreadCount: 2 },
      { helpId: 'h2', unreadCount: 3 },
    ])
    const store = useRescueSessionStore()

    await store.loadMineSessions()

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
