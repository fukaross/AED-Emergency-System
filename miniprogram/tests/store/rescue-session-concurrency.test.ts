import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { RescueMessage, RescueSessionSnapshot } from '@/types/rescue-session'
import {
  buildMessage,
  buildSnapshot,
  deferred,
} from './rescue-session-test-fixtures'

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
    [{ statusCode: 401 }, 'revoked'],
    [{ code: 1002 }, 'revoked'],
    [{ statusCode: 403 }, 'revoked'],
    [{ code: 1003 }, 'revoked'],
    [{ code: 1018 }, 'revoked'],
    [{ code: 1017 }, 'revoked'],
    [{ code: 1016 }, 'closed'],
    [{ statusCode: 500 }, null],
  ] as const)('classifies %o as %s', (error, expected) => {
    expect(classifyRescueSessionTerminalError(error)).toBe(expected)
  })
})
