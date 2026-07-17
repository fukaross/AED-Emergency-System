import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { RescueSocketHandlers, RescueSocketHandle } from '@/services/rescue-socket'

const socketMocks = vi.hoisted(() => ({ connectRescueSocket: vi.fn() }))

vi.mock('@/services/rescue-socket', () => socketMocks)
vi.mock('vue', async () => ({
  ...await vi.importActual<Record<string, unknown>>('vue'),
  onUnmounted: vi.fn(),
}))

import { useSessionRealtime } from '@/composables/useSessionRealtime'

async function flushPromises(): Promise<void> {
  for (let index = 0; index < 5; index += 1) await Promise.resolve()
}

function installSocketHandle(): { handle: RescueSocketHandle; handlers: () => RescueSocketHandlers } {
  let currentHandlers!: RescueSocketHandlers
  const handle: RescueSocketHandle = {
    close: vi.fn(),
    isConnected: vi.fn().mockReturnValue(false),
  }
  socketMocks.connectRescueSocket.mockImplementation(
    (_helpId: string, handlers: RescueSocketHandlers) => {
      currentHandlers = handlers
      return handle
    },
  )
  return { handle, handlers: () => currentHandlers }
}

describe('useSessionRealtime', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    socketMocks.connectRescueSocket.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  it('catches up over HTTP when the socket opens', async () => {
    const socket = installSocketHandle()
    const refresh = vi.fn().mockResolvedValue(undefined)
    const realtime = useSessionRealtime({
      getHelpId: () => 'help-1',
      onEnvelope: vi.fn(),
      onPollRefresh: refresh,
      onSignalRefresh: vi.fn(),
    })

    realtime.start()
    socket.handlers().onOpen?.()
    await flushPromises()

    expect(refresh).toHaveBeenCalledTimes(1)
    expect(realtime.usingSocket.value).toBe(true)
  })

  it('falls back immediately and idempotently after an async connection failure', async () => {
    const socket = installSocketHandle()
    const refresh = vi.fn().mockResolvedValue(undefined)
    const realtime = useSessionRealtime({
      getHelpId: () => 'help-1',
      onEnvelope: vi.fn(),
      onPollRefresh: refresh,
      onSignalRefresh: vi.fn(),
    })

    realtime.start()
    socket.handlers().onUnavailable?.()
    socket.handlers().onUnavailable?.()
    await flushPromises()

    expect(refresh).toHaveBeenCalledTimes(1)
    expect(realtime.usingSocket.value).toBe(false)
  })

  it('retries a failed on-open catch-up while the socket stays healthy', async () => {
    const socket = installSocketHandle()
    const refresh = vi.fn()
      .mockRejectedValueOnce(new Error('temporary network failure'))
      .mockResolvedValueOnce(undefined)
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const realtime = useSessionRealtime({
      getHelpId: () => 'help-1',
      onEnvelope: vi.fn(),
      onPollRefresh: refresh,
      onSignalRefresh: vi.fn(),
    })

    realtime.start()
    socket.handlers().onOpen?.()
    await flushPromises()
    vi.advanceTimersByTime(3_000)
    await flushPromises()

    expect(refresh).toHaveBeenCalledTimes(2)
    expect(realtime.usingSocket.value).toBe(true)
  })

  it('acks visible socket messages with a coalesced HTTP refresh', async () => {
    const socket = installSocketHandle()
    const refresh = vi.fn().mockResolvedValue(undefined)
    const realtime = useSessionRealtime({
      getHelpId: () => 'help-1',
      onEnvelope: vi.fn(),
      onPollRefresh: refresh,
      onSignalRefresh: vi.fn(),
    })
    realtime.start()
    socket.handlers().onOpen?.()
    await flushPromises()
    refresh.mockClear()

    socket.handlers().onEnvelope({ type: 'message', data: {} })
    socket.handlers().onEnvelope({ type: 'message', data: {} })
    await flushPromises()

    expect(refresh).toHaveBeenCalledTimes(1)
  })

  it('advances local participant signals while the socket remains healthy', () => {
    const socket = installSocketHandle()
    const onSignalRefresh = vi.fn()
    const realtime = useSessionRealtime({
      getHelpId: () => 'help-1',
      onEnvelope: vi.fn(),
      onPollRefresh: vi.fn().mockResolvedValue(undefined),
      onSignalRefresh,
    })

    realtime.start()
    socket.handlers().onOpen?.()
    const callsAfterStart = onSignalRefresh.mock.calls.length
    vi.advanceTimersByTime(2_000)

    expect(onSignalRefresh.mock.calls.length).toBeGreaterThan(callsAfterStart)
  })

  it('ignores envelopes from a stopped socket generation', () => {
    const socket = installSocketHandle()
    const onEnvelope = vi.fn()
    const realtime = useSessionRealtime({
      getHelpId: () => 'help-1',
      onEnvelope,
      onPollRefresh: vi.fn().mockResolvedValue(undefined),
      onSignalRefresh: vi.fn(),
    })

    realtime.start()
    realtime.stop()
    socket.handlers().onEnvelope({ type: 'message', data: {} })

    expect(onEnvelope).not.toHaveBeenCalled()
  })
})
