import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { SessionResumeController } from '@/pages-sub/rescue/session-resume-controller'

interface Deferred<T> {
  readonly promise: Promise<T>
  readonly resolve: (value: T) => void
}

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((done) => { resolve = done })
  return { promise, resolve }
}

describe('SessionResumeController', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime('2026-07-17T12:00:00.000Z')
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('expires local coordinates before waiting for the HTTP refresh', async () => {
    const refresh = deferred<boolean>()
    const participant = { signal: 'ok', location: { lat: 1, lng: 2 } } as {
      signal: string
      location: { lat: number; lng: number } | null
    }
    const startRuntime = vi.fn()
    const controller = new SessionResumeController({
      refreshSignals: vi.fn(() => {
        participant.signal = 'gone'
        participant.location = null
      }),
      refreshSession: vi.fn(() => refresh.promise),
      canResume: () => true,
      startRuntime,
      handleTerminalError: () => false,
      handleRecoverableError: vi.fn(),
    })

    const pending = controller.resume('help-1')

    expect(participant).toEqual({ signal: 'gone', location: null })
    expect(startRuntime).not.toHaveBeenCalled()
    refresh.resolve(true)
    await pending
    expect(startRuntime).toHaveBeenCalledTimes(1)
  })

  it('does not restart runtime after the page invalidates a pending refresh', async () => {
    const refresh = deferred<boolean>()
    const startRuntime = vi.fn()
    const controller = new SessionResumeController({
      refreshSignals: vi.fn(),
      refreshSession: vi.fn(() => refresh.promise),
      canResume: () => true,
      startRuntime,
      handleTerminalError: () => false,
      handleRecoverableError: vi.fn(),
    })

    const pending = controller.resume('help-1')
    controller.invalidate()
    refresh.resolve(true)
    await pending

    expect(startRuntime).not.toHaveBeenCalled()
  })
})
