import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const geoMocks = vi.hoisted(() => ({
  getCurrentLocation: vi.fn(),
}))

vi.mock('@/services/geo-service', () => geoMocks)
vi.mock('vue', async () => ({
  ...await vi.importActual<Record<string, unknown>>('vue'),
  onUnmounted: vi.fn(),
}))

import { useSessionLocationReporter } from '@/composables/useSessionLocationReporter'

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

async function flushPromises(): Promise<void> {
  for (let index = 0; index < 5; index += 1) await Promise.resolve()
}

describe('useSessionLocationReporter', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    geoMocks.getCurrentLocation.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('does not report an in-flight geolocation result after stop', async () => {
    const location = deferred<{
      success: true
      point: { lat: number; lng: number }
    }>()
    geoMocks.getCurrentLocation.mockReturnValueOnce(location.promise)
    const report = vi.fn().mockResolvedValue(undefined)
    const reporter = useSessionLocationReporter(report)

    reporter.start()
    reporter.stop()
    location.resolve({ success: true, point: { lat: 19.97, lng: 110.51 } })
    await flushPromises()

    expect(report).not.toHaveBeenCalled()
  })

  it('keeps a restarted run independent from the previous pending tick', async () => {
    const staleLocation = deferred<{
      success: true
      point: { lat: number; lng: number }
    }>()
    geoMocks.getCurrentLocation
      .mockReturnValueOnce(staleLocation.promise)
      .mockResolvedValueOnce({ success: true, point: { lat: 19.98, lng: 110.52 } })
    const report = vi.fn().mockResolvedValue(undefined)
    const reporter = useSessionLocationReporter(report)

    reporter.start()
    reporter.stop()
    reporter.start()
    await flushPromises()
    staleLocation.resolve({ success: true, point: { lat: 19.97, lng: 110.51 } })
    await flushPromises()

    expect(report).toHaveBeenCalledTimes(1)
    expect(report).toHaveBeenCalledWith({ lat: 19.98, lng: 110.52 })
  })
})
