import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { RescueMessage, RescueSessionSnapshot } from '@/types/rescue-session'

const apiMocks = vi.hoisted(() => ({
  joinRescueSession: vi.fn(),
  fetchRescueSession: vi.fn(),
  postRescueMessage: vi.fn(),
  postRescueStatusEvent: vi.fn(),
  reportRescueLocation: vi.fn(),
  fetchMineRescueSessions: vi.fn(),
}))

vi.mock('@/api/rescue-session', () => apiMocks)

import {
  classifyRescueSessionTerminalError,
  useRescueSessionStore,
} from '@/store/rescue-session'

interface Deferred<T> {
  readonly promise: Promise<T>
  readonly resolve: (value: T) => void
}

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((done) => {
    resolve = done
  })
  return { promise, resolve }
}

function buildMessage(seq: number): RescueMessage {
  return {
    id: `message-${seq}`,
    seq,
    kind: 'text',
    senderOpenId: 'rescuer-a',
    senderName: '急救员 A',
    text: `消息 ${seq}`,
    eventType: null,
    action: null,
    createdAt: '2026-07-17T10:00:00.000Z',
  }
}

function buildSnapshot(helpId: string): RescueSessionSnapshot {
  return {
    helpId,
    requestNo: helpId,
    helpStatus: 'responding',
    sos: { lat: 19.97, lng: 110.51, address: '测试点' },
    me: { openId: 'rescuer-a', takenAed: false, arrived: false, lastReadSeq: 0 },
    participants: [
      {
        openId: 'rescuer-a',
        displayName: '急救员 A',
        joinedAt: '2026-07-17T09:59:00.000Z',
        takenAed: false,
        arrived: false,
        signal: 'ok',
        location: {
          lat: 19.971,
          lng: 110.511,
          reportedAt: '2026-07-17T10:00:00.000Z',
        },
      },
    ],
    messages: [buildMessage(1)],
    latestSeq: 1,
    serverTime: '2026-07-17T10:00:01.000Z',
  }
}

describe('rescue-session store request isolation', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    Object.values(apiMocks).forEach((mock) => mock.mockReset())
  })

  it('ignores a late join response from the previous help', async () => {
    const oldJoin = deferred<RescueSessionSnapshot>()
    apiMocks.joinRescueSession
      .mockReturnValueOnce(oldJoin.promise)
      .mockResolvedValueOnce(buildSnapshot('help-b'))
    const store = useRescueSessionStore()

    const oldRequest = store.joinSession('help-a')
    await store.joinSession('help-b')
    oldJoin.resolve(buildSnapshot('help-a'))
    await oldRequest

    expect(store.snapshot?.helpId).toBe('help-b')
  })

  it('clears the previous snapshot immediately when switching help', async () => {
    const nextJoin = deferred<RescueSessionSnapshot>()
    apiMocks.joinRescueSession
      .mockResolvedValueOnce(buildSnapshot('help-a'))
      .mockReturnValueOnce(nextJoin.promise)
    const store = useRescueSessionStore()
    await store.joinSession('help-a')

    const pendingJoin = store.joinSession('help-b')

    expect(store.snapshot).toBeNull()
    expect(store.sessionClosed).toBe(false)
    nextJoin.resolve(buildSnapshot('help-b'))
    await pendingJoin
  })

  it('does not let an unloading old page reset the newly active help', async () => {
    apiMocks.joinRescueSession
      .mockResolvedValueOnce(buildSnapshot('help-b'))
      .mockResolvedValueOnce(buildSnapshot('help-a'))
    const store = useRescueSessionStore()
    await store.joinSession('help-b')
    await store.joinSession('help-a')

    store.resetSession('help-b')

    expect(store.snapshot?.helpId).toBe('help-a')
  })

  it('ignores a late refresh after another help becomes active', async () => {
    const oldRefresh = deferred<RescueSessionSnapshot>()
    apiMocks.joinRescueSession
      .mockResolvedValueOnce(buildSnapshot('help-a'))
      .mockResolvedValueOnce(buildSnapshot('help-b'))
    apiMocks.fetchRescueSession.mockReturnValueOnce(oldRefresh.promise)
    const store = useRescueSessionStore()
    await store.joinSession('help-a')

    const pendingRefresh = store.refreshSession('help-a')
    await store.joinSession('help-b')
    oldRefresh.resolve({ ...buildSnapshot('help-a'), messages: [buildMessage(2)], latestSeq: 2 })
    await pendingRefresh

    expect(store.snapshot?.helpId).toBe('help-b')
    expect(store.snapshot?.messages).toHaveLength(1)
  })

  it('does not append a late message response to another help', async () => {
    const oldMessage = deferred<RescueMessage>()
    apiMocks.joinRescueSession
      .mockResolvedValueOnce(buildSnapshot('help-a'))
      .mockResolvedValueOnce(buildSnapshot('help-b'))
    apiMocks.postRescueMessage.mockReturnValueOnce(oldMessage.promise)
    const store = useRescueSessionStore()
    await store.joinSession('help-a')

    const pendingSend = store.sendMessage('正在前往')
    await store.joinSession('help-b')
    oldMessage.resolve(buildMessage(2))
    await pendingSend

    expect(store.snapshot?.helpId).toBe('help-b')
    expect(store.snapshot?.messages).toHaveLength(1)
  })

  it('rejects envelopes from a socket bound to another help', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-b'))
    const store = useRescueSessionStore()
    await store.joinSession('help-b')

    store.applySocketEnvelope('help-a', { type: 'message', data: buildMessage(2) })

    expect(store.snapshot?.messages).toHaveLength(1)
  })
})

describe('rescue-session terminal errors', () => {
  it.each([
    [{ statusCode: 403 }, 'revoked'],
    [{ code: 1003 }, 'revoked'],
    [{ code: 1017 }, 'revoked'],
    [{ code: 1016 }, 'closed'],
    [{ statusCode: 500 }, null],
  ] as const)('classifies %o as %s', (error, expected) => {
    expect(classifyRescueSessionTerminalError(error)).toBe(expected)
  })
})

describe('rescue-session store location privacy', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    Object.values(apiMocks).forEach((mock) => mock.mockReset())
  })

  it('derives signal age from location.reportedAt and removes expired coordinates', async () => {
    const nowSpy = vi.spyOn(Date, 'now').mockReturnValue(
      Date.parse('2026-07-17T10:00:01.000Z'),
    )
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    const store = useRescueSessionStore()
    await store.joinSession('help-a')
    nowSpy.mockRestore()

    store.refreshParticipantSignals(Date.parse('2026-07-17T10:01:00.000Z'))
    expect(store.snapshot?.participants[0].signal).toBe('lost')
    expect(store.snapshot?.participants[0].location).not.toBeNull()

    store.refreshParticipantSignals(Date.parse('2026-07-17T10:01:31.000Z'))
    expect(store.snapshot?.participants[0].signal).toBe('gone')
    expect(store.snapshot?.participants[0].location).toBeNull()
  })

  it('calibrates TTL against snapshot.serverTime when the device clock drifts', async () => {
    const localJoinTime = Date.parse('2026-07-17T12:00:01.000Z')
    const nowSpy = vi.spyOn(Date, 'now').mockReturnValue(localJoinTime)
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    const store = useRescueSessionStore()
    await store.joinSession('help-a')
    nowSpy.mockRestore()

    store.refreshParticipantSignals(Date.parse('2026-07-17T12:00:31.000Z'))
    expect(store.snapshot?.participants[0].signal).toBe('ok')

    store.refreshParticipantSignals(Date.parse('2026-07-17T12:01:01.000Z'))
    expect(store.snapshot?.participants[0].signal).toBe('lost')
  })

  it('clears every exact coordinate as soon as help_status closes', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    const store = useRescueSessionStore()
    await store.joinSession('help-a')

    store.applySocketEnvelope('help-a', {
      type: 'help_status',
      data: { helpStatus: 'closed' },
    })

    expect(store.sessionClosed).toBe(true)
    expect(store.snapshot?.participants[0]).toMatchObject({ signal: 'gone', location: null })
  })

  it('does not let an in-flight active refresh reopen a closed session', async () => {
    const refresh = deferred<RescueSessionSnapshot>()
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    apiMocks.fetchRescueSession.mockReturnValueOnce(refresh.promise)
    const store = useRescueSessionStore()
    await store.joinSession('help-a')

    const pendingRefresh = store.refreshSession('help-a')
    store.applySocketEnvelope('help-a', {
      type: 'help_status',
      data: { helpStatus: 'closed' },
    })
    refresh.resolve(buildSnapshot('help-a'))
    await pendingRefresh

    expect(store.sessionClosed).toBe(true)
    expect(store.snapshot?.helpStatus).toBe('closed')
    expect(store.snapshot?.participants[0].location).toBeNull()
  })

  it('ignores late message and participant results after closure', async () => {
    const message = deferred<RescueMessage>()
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    apiMocks.postRescueMessage.mockReturnValueOnce(message.promise)
    const store = useRescueSessionStore()
    await store.joinSession('help-a')

    const pendingMessage = store.sendMessage('马上到达')
    store.applySocketEnvelope('help-a', {
      type: 'help_status',
      data: { helpStatus: 'closed' },
    })
    store.applySocketEnvelope('help-a', {
      type: 'participant',
      data: buildSnapshot('help-a').participants[0],
    })
    message.resolve(buildMessage(2))
    await pendingMessage

    expect(store.snapshot?.messages).toHaveLength(1)
    expect(store.snapshot?.participants[0].location).toBeNull()
  })
})
