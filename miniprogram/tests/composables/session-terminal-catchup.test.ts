import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { RescueSocketHandle, RescueSocketHandlers } from '@/services/rescue-socket'
import type { RescueSocketEnvelope } from '@/types/rescue-session'
import { buildSnapshot, deferred } from '@/tests/store/rescue-session-test-fixtures'

const socketMocks = vi.hoisted(() => ({ connectRescueSocket: vi.fn() }))
const apiMocks = vi.hoisted(() => ({
  joinRescueSession: vi.fn(),
  fetchRescueSession: vi.fn(),
  postRescueMessage: vi.fn(),
  postRescueStatusEvent: vi.fn(),
  reportRescueLocation: vi.fn(),
  fetchMineRescueSessions: vi.fn(),
}))

vi.mock('@/services/rescue-socket', () => socketMocks)
vi.mock('@/api/rescue-session', () => apiMocks)
vi.mock('vue', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('vue')),
  onUnmounted: vi.fn(),
}))

import { useSessionRealtime } from '@/composables/useSessionRealtime'
import { useRescueSessionStore } from '@/store/rescue-session'

interface SocketHarness {
  readonly handle: RescueSocketHandle
  readonly handlers: () => RescueSocketHandlers
}

function installSocket(): SocketHarness {
  let handlers!: RescueSocketHandlers
  const handle = { close: vi.fn(), isConnected: vi.fn(() => true) }
  socketMocks.connectRescueSocket.mockImplementation(
    (_id: string, nextHandlers: RescueSocketHandlers) => {
      handlers = nextHandlers
      return handle
    },
  )
  return { handle, handlers: () => handlers }
}

const CLOSE_HINT: RescueSocketEnvelope = {
  type: 'help_status',
  data: { helpStatus: 'closed' },
}

describe('session terminal websocket handling', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime('2026-07-17T10:00:01.000Z')
    setActivePinia(createPinia())
    socketMocks.connectRescueSocket.mockReset()
    Object.values(apiMocks).forEach((mock) => mock.mockReset())
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  it('commits the final frame and stops retries while HTTP stays offline', async () => {
    const socket = installSocket()
    const store = useRescueSessionStore()
    const stopLocation = vi.fn()
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    apiMocks.fetchRescueSession.mockRejectedValue(new Error('offline'))
    await store.joinSession('help-a')
    const realtime = createRealtime(store, stopLocation)
    realtime.start()
    socket.handlers().onOpen?.()
    await vi.advanceTimersByTimeAsync(0)

    socket.handlers().onEnvelope(CLOSE_HINT)

    expect(store.sessionClosed).toBe(true)
    expect(store.snapshot?.helpStatus).toBe('closed')
    expect(store.snapshot?.participants[0].location).toBeNull()
    expect(stopLocation).toHaveBeenCalledTimes(1)
    expect(socket.handle.close).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(9_000)
    expect(apiMocks.fetchRescueSession).toHaveBeenCalledTimes(1)
  })

  it('does not let an older in-flight active snapshot reopen the final frame', async () => {
    const refresh = deferred<ReturnType<typeof buildSnapshot>>()
    const socket = installSocket()
    const store = useRescueSessionStore()
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    apiMocks.fetchRescueSession.mockReturnValueOnce(refresh.promise)
    await store.joinSession('help-a')
    const realtime = createRealtime(store, vi.fn())
    realtime.start()
    socket.handlers().onOpen?.()
    await vi.advanceTimersByTimeAsync(0)

    socket.handlers().onEnvelope(CLOSE_HINT)
    refresh.resolve(buildSnapshot('help-a'))
    await vi.advanceTimersByTimeAsync(0)

    expect(store.sessionClosed).toBe(true)
    expect(store.snapshot?.helpStatus).toBe('closed')
    expect(apiMocks.fetchRescueSession).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(9_000)
    expect(apiMocks.fetchRescueSession).toHaveBeenCalledTimes(1)
  })
})

function createRealtime(store: ReturnType<typeof useRescueSessionStore>, stopLocation: () => void) {
  const realtime = useSessionRealtime({
    getHelpId: () => 'help-a',
    onEnvelope: (envelope, helpId) => {
      const applied = store.applySocketEnvelope(helpId, envelope)
      if (!applied || !store.sessionClosed) return
      stopLocation()
      realtime.stop()
    },
    onPollRefresh: async (helpId) => {
      await store.refreshSession(helpId)
    },
    onSignalRefresh: (now) => store.refreshParticipantSignals(now),
  })
  return realtime
}
