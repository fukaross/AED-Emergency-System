import { ref } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { HelpRequestDetail } from '@/types/help'
import type { RescueView } from '@/types/rescue-session'

const apiMocks = vi.hoisted(() => ({ fetchRescueView: vi.fn() }))
vi.mock('@/api/rescue-session', () => ({ fetchRescueView: apiMocks.fetchRescueView }))

import { useHelpRescueView } from '@/pages-sub/help/useHelpRescueView'

const SERVER_NOW = Date.parse('2026-07-17T10:00:00.000Z')

function buildDetail(): HelpRequestDetail {
  return { id: 'help-1', status: 'responding' } as HelpRequestDetail
}

function buildView(overrides: Partial<RescueView> = {}): RescueView {
  return {
    helpId: 'help-1',
    helpStatus: 'responding',
    sos: { lat: 31.23, lng: 121.47, address: '测试点' },
    respondersCount: 1,
    participants: [{
      participantId: 'participant-1',
      displayName: '急救员一',
      takenAed: false,
      arrived: false,
      signal: 'ok',
      location: {
        lat: 31.24,
        lng: 121.48,
        reportedAt: new Date(SERVER_NOW).toISOString(),
      },
    }],
    events: [],
    serverTime: new Date(SERVER_NOW).toISOString(),
    timing: {
      signalLostAfterSeconds: 2,
      locationGoneAfterSeconds: 4,
      locationReportIntervalSeconds: 1,
    },
    ...overrides,
  }
}

async function flushPromises(): Promise<void> {
  for (let index = 0; index < 5; index += 1) await Promise.resolve()
}

describe('help rescue view runtime TTL', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(SERVER_NOW + 2 * 60 * 60 * 1000)
    apiMocks.fetchRescueView.mockReset()
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  it('advances fresh, lost and gone every second using server clock drift', async () => {
    apiMocks.fetchRescueView.mockResolvedValueOnce(buildView())
    const rescue = useHelpRescueView(ref(buildDetail()))

    expect(await rescue.refreshRescueView()).toBe('active')
    rescue.startRescuePolling()
    vi.advanceTimersByTime(3000)
    expect(rescue.rescueView.value?.participants[0].signal).toBe('lost')

    vi.advanceTimersByTime(2000)
    expect(rescue.rescueView.value?.participants[0]).toMatchObject({
      signal: 'gone',
      location: null,
    })
    rescue.suspendRescueView()
  })

  it('keeps advancing TTL while a polling failure marks the view stale', async () => {
    apiMocks.fetchRescueView
      .mockResolvedValueOnce(buildView())
      .mockRejectedValueOnce(new Error('offline'))
    const rescue = useHelpRescueView(ref(buildDetail()))
    await rescue.refreshRescueView()
    rescue.startRescuePolling()

    vi.advanceTimersByTime(8000)
    await flushPromises()

    expect(rescue.rescueViewStale.value).toBe(true)
    expect(rescue.rescueView.value?.participants[0]).toMatchObject({
      signal: 'gone',
      location: null,
    })
    rescue.suspendRescueView()
  })

  it('clears stale state after the next successful refresh', async () => {
    apiMocks.fetchRescueView
      .mockResolvedValueOnce(buildView())
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce(buildView({ serverTime: new Date(SERVER_NOW + 1000).toISOString() }))
    const rescue = useHelpRescueView(ref(buildDetail()))

    await rescue.refreshRescueView()
    await rescue.refreshRescueView()
    expect(rescue.rescueViewStale.value).toBe(true)
    await rescue.refreshRescueView()

    expect(rescue.rescueViewStale.value).toBe(false)
    rescue.suspendRescueView()
  })

  it.each([
    { statusCode: 401 },
    { statusCode: 403 },
    { statusCode: 409 },
    { code: 1002 },
    { code: 1017 },
  ])('never restarts polling after terminal error %o', async (error) => {
    apiMocks.fetchRescueView.mockRejectedValueOnce(error)
    const rescue = useHelpRescueView(ref(buildDetail()))

    expect(await rescue.refreshRescueView()).toBe('terminal')
    expect(rescue.startRescuePolling()).toBe(false)
    vi.advanceTimersByTime(16_000)
    await flushPromises()

    expect(apiMocks.fetchRescueView).toHaveBeenCalledTimes(1)
    expect(rescue.rescueView.value).toBeNull()
  })

  it('treats a closed response as terminal and cannot restart it', async () => {
    apiMocks.fetchRescueView.mockResolvedValueOnce(buildView({ helpStatus: 'closed' }))
    const rescue = useHelpRescueView(ref(buildDetail()))

    expect(await rescue.refreshRescueView()).toBe('terminal')
    expect(rescue.startRescuePolling()).toBe(false)
    vi.advanceTimersByTime(16_000)
    await flushPromises()

    expect(apiMocks.fetchRescueView).toHaveBeenCalledTimes(1)
  })
})
