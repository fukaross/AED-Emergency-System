import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const geoMocks = vi.hoisted(() => ({ getCurrentLocation: vi.fn() }))

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
  const promise = new Promise<T>((done) => { resolve = done })
  return { promise, resolve }
}

async function flushPromises(): Promise<void> {
  for (let index = 0; index < 5; index += 1) await Promise.resolve()
}

const LIVE_LOCATION = {
  success: true,
  point: { lat: 31.24, lng: 121.48 },
  isDefault: false,
  showSystemLocation: true,
} as const

const DENIED_LOCATION = {
  success: false,
  point: { lat: 31.23, lng: 121.47 },
  isDefault: true,
  showSystemLocation: false,
  failureReason: 'permission_denied',
  errorMessage: '定位权限被拒绝',
} as const

describe('useSessionLocationReporter', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    geoMocks.getCurrentLocation.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('does not report or change health after a pending location resolves after stop', async () => {
    const location = deferred<typeof LIVE_LOCATION>()
    geoMocks.getCurrentLocation.mockReturnValueOnce(location.promise)
    const report = vi.fn().mockResolvedValue(undefined)
    const reporter = useSessionLocationReporter({ report })

    reporter.start(5)
    reporter.stop()
    location.resolve(LIVE_LOCATION)
    await flushPromises()

    expect(report).not.toHaveBeenCalled()
    expect(reporter.health.value.status).toBe('idle')
  })

  it('does not let a stopped report completion overwrite the idle state', async () => {
    const reportResult = deferred<void>()
    geoMocks.getCurrentLocation.mockResolvedValueOnce(LIVE_LOCATION)
    const report = vi.fn().mockReturnValueOnce(reportResult.promise)
    const reporter = useSessionLocationReporter({ report })

    reporter.start(5)
    await flushPromises()
    reporter.stop()
    reportResult.resolve()
    await flushPromises()

    expect(report).toHaveBeenCalledTimes(1)
    expect(reporter.health.value.status).toBe('idle')
  })

  it('uses the server-provided report interval', async () => {
    geoMocks.getCurrentLocation.mockResolvedValue(LIVE_LOCATION)
    const report = vi.fn().mockResolvedValue(undefined)
    const reporter = useSessionLocationReporter({ report })

    reporter.start(2)
    await flushPromises()
    vi.advanceTimersByTime(1999)
    await flushPromises()
    expect(report).toHaveBeenCalledTimes(1)

    vi.advanceTimersByTime(1)
    await flushPromises()
    expect(report).toHaveBeenCalledTimes(2)
  })

  it('never uploads the campus fallback and notifies permission denial once', async () => {
    geoMocks.getCurrentLocation.mockResolvedValue(DENIED_LOCATION)
    const report = vi.fn().mockResolvedValue(undefined)
    const onPermissionDenied = vi.fn()
    const reporter = useSessionLocationReporter({ report, onPermissionDenied })

    reporter.start(1)
    await flushPromises()
    vi.advanceTimersByTime(2000)
    await flushPromises()

    expect(report).not.toHaveBeenCalled()
    expect(reporter.health.value.status).toBe('permission_denied')
    expect(onPermissionDenied).toHaveBeenCalledTimes(1)
  })

  it('clears permission and network health after reporting recovers', async () => {
    geoMocks.getCurrentLocation
      .mockResolvedValueOnce(DENIED_LOCATION)
      .mockResolvedValue(LIVE_LOCATION)
    const report = vi.fn()
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValue(undefined)
    const reporter = useSessionLocationReporter({ report })

    reporter.start(1)
    await flushPromises()
    expect(reporter.health.value.status).toBe('permission_denied')

    vi.advanceTimersByTime(1000)
    await flushPromises()
    expect(reporter.health.value.status).toBe('network_error')

    vi.advanceTimersByTime(1000)
    await flushPromises()
    expect(reporter.health.value.status).toBe('healthy')
  })

  it('keeps a restarted run independent from the previous pending tick', async () => {
    const staleLocation = deferred<typeof LIVE_LOCATION>()
    geoMocks.getCurrentLocation
      .mockReturnValueOnce(staleLocation.promise)
      .mockResolvedValueOnce(LIVE_LOCATION)
    const report = vi.fn().mockResolvedValue(undefined)
    const reporter = useSessionLocationReporter({ report })

    reporter.start(5)
    reporter.stop()
    reporter.start(5)
    await flushPromises()
    staleLocation.resolve(LIVE_LOCATION)
    await flushPromises()

    expect(report).toHaveBeenCalledTimes(1)
    expect(report).toHaveBeenCalledWith(LIVE_LOCATION.point)
  })
})
