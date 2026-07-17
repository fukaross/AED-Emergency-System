import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { AdminRescueSessionDetail } from '@/types/rescue-session'
import { useRescueSessionDetail } from './use-rescue-session-detail'

vi.mock('@/api/rescue-sessions', () => ({ getRescueSessionDetail: vi.fn() }))
vi.mock('@/api/client', () => ({
  getApiErrorMessage: (_error: unknown, fallback: string) => fallback,
}))

function buildDetail(helpStatus = 'published', requestNo = 'SOS-NEW'): AdminRescueSessionDetail {
  return {
    helpId: 'help-1',
    requestNo,
    helpStatus,
    sos: { lat: 19.98, lng: 110.52, address: '教学楼' },
    requester: { openId: 'requester-1', displayName: '求助人' },
    closedAt: helpStatus === 'closed' ? '2026-07-17T10:01:00Z' : null,
    closeNote: null,
    participants: [],
    messages: [],
    serverTime: '2026-07-17T10:00:00Z',
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise
  })
  return { promise, resolve }
}

beforeEach(() => {
  vi.useFakeTimers()
  vi.spyOn(console, 'error').mockImplementation(() => undefined)
})

afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

describe('useRescueSessionDetail request ordering', () => {
  it('aborts and ignores an older response when a newer request wins', async () => {
    const first = deferred<AdminRescueSessionDetail>()
    const second = deferred<AdminRescueSessionDetail>()
    const signals: AbortSignal[] = []
    const fetchDetail = vi.fn((_helpId: string, signal: AbortSignal) => {
      signals.push(signal)
      return signals.length === 1 ? first.promise : second.promise
    })
    const state = useRescueSessionDetail({ helpId: 'help-1', fetchDetail })

    const firstRequest = state.pollData()
    const secondRequest = state.pollData()
    expect(signals[0].aborted).toBe(true)
    second.resolve(buildDetail('published', 'SOS-NEW'))
    await secondRequest
    first.resolve(buildDetail('published', 'SOS-OLD'))
    await firstRequest

    expect(state.detail.value?.requestNo).toBe('SOS-NEW')
  })
})

describe('useRescueSessionDetail route reuse', () => {
  it('aborts the old session and switches polling to the new help id', async () => {
    const oldRequest = deferred<AdminRescueSessionDetail>()
    const newRequest = deferred<AdminRescueSessionDetail>()
    const signals: AbortSignal[] = []
    const fetchDetail = vi.fn((helpId: string, signal: AbortSignal) => {
      signals.push(signal)
      return helpId === 'help-old' ? oldRequest.promise : newRequest.promise
    })
    const state = useRescueSessionDetail({ helpId: 'help-old', fetchDetail })

    const oldLoad = state.loadData()
    const switchRequest = state.setHelpId('help-new')
    expect(signals[0].aborted).toBe(true)
    expect(fetchDetail.mock.calls.map(([helpId]) => helpId)).toEqual(['help-old', 'help-new'])

    newRequest.resolve({ ...buildDetail(), helpId: 'help-new', requestNo: 'SOS-NEW' })
    await switchRequest
    oldRequest.resolve({ ...buildDetail(), helpId: 'help-old', requestNo: 'SOS-OLD' })
    await oldLoad

    expect(state.detail.value?.helpId).toBe('help-new')
    expect(state.detail.value?.requestNo).toBe('SOS-NEW')
    await vi.advanceTimersByTimeAsync(10_000)
    expect(fetchDetail.mock.calls.at(-1)?.[0]).toBe('help-new')
  })
})

describe('useRescueSessionDetail closure lifecycle', () => {
  it('pauses while hidden, refreshes on resume, and stops permanently after closure', async () => {
    const fetchDetail = vi.fn()
      .mockResolvedValueOnce(buildDetail())
      .mockResolvedValueOnce(buildDetail())
      .mockResolvedValueOnce(buildDetail('closed'))
    const state = useRescueSessionDetail({
      helpId: 'help-1', fetchDetail, pollIntervalMs: 1_000,
    })

    await state.loadData()
    state.startPolling()
    await state.setVisible(false)
    await vi.advanceTimersByTimeAsync(2_000)
    expect(fetchDetail).toHaveBeenCalledTimes(1)
    await state.setVisible(true)
    expect(fetchDetail).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(1_000)
    expect(fetchDetail).toHaveBeenCalledTimes(3)
    expect(state.detail.value?.helpStatus).toBe('closed')
    await vi.advanceTimersByTimeAsync(2_000)
    expect(fetchDetail).toHaveBeenCalledTimes(3)
  })
})

describe('useRescueSessionDetail initial visibility', () => {
  it('does not load or poll until an initially hidden view becomes visible', async () => {
    const fetchDetail = vi.fn().mockResolvedValue(buildDetail())
    const state = useRescueSessionDetail({
      helpId: 'help-1',
      fetchDetail,
      initiallyVisible: false,
      pollIntervalMs: 1_000,
    })

    await state.loadData()
    state.startPolling()
    await vi.advanceTimersByTimeAsync(2_000)
    expect(fetchDetail).not.toHaveBeenCalled()
    await state.setVisible(true)
    expect(fetchDetail).toHaveBeenCalledTimes(1)
  })
})

describe('useRescueSessionDetail polling generation', () => {
  it('does not let a paused polling callback schedule over the resumed request', async () => {
    const oldPoll = deferred<AdminRescueSessionDetail>()
    const resumedPoll = deferred<AdminRescueSessionDetail>()
    const fetchDetail = vi.fn()
      .mockResolvedValueOnce(buildDetail())
      .mockImplementationOnce(() => oldPoll.promise)
      .mockImplementationOnce(() => resumedPoll.promise)
      .mockResolvedValue(buildDetail())
    const state = useRescueSessionDetail({
      helpId: 'help-1', fetchDetail, pollIntervalMs: 1_000,
    })

    await state.loadData()
    state.startPolling()
    vi.advanceTimersByTime(1_000)
    await Promise.resolve()
    await state.setVisible(false)
    const resume = state.setVisible(true)
    oldPoll.resolve(buildDetail())
    await vi.advanceTimersByTimeAsync(1_000)

    expect(fetchDetail).toHaveBeenCalledTimes(3)
    resumedPoll.resolve(buildDetail())
    await resume
  })
})

describe('useRescueSessionDetail error state', () => {
  it('shows initial errors but keeps the last good detail on background failures', async () => {
    const fetchDetail = vi.fn()
      .mockRejectedValueOnce(new Error('initial failed'))
      .mockResolvedValueOnce(buildDetail())
      .mockRejectedValueOnce(new Error('poll failed'))
    const state = useRescueSessionDetail({ helpId: 'help-1', fetchDetail })

    await state.loadData()
    expect(state.errorMessage.value).toBe('加载急救会话失败')
    await state.loadData()
    expect(state.errorMessage.value).toBe('')
    await state.pollData()
    expect(state.detail.value?.requestNo).toBe('SOS-NEW')
    expect(state.errorMessage.value).toBe('')
  })
})

describe('useRescueSessionDetail resume error', () => {
  it('uses an error-reporting load when a view resumes without a snapshot', async () => {
    const fetchDetail = vi.fn().mockRejectedValue(new Error('resume failed'))
    const state = useRescueSessionDetail({
      helpId: 'help-1', fetchDetail, pollIntervalMs: 1_000,
    })

    await state.setVisible(false)
    await state.setVisible(true)

    expect(state.errorMessage.value).toBe('加载急救会话失败')
    await vi.advanceTimersByTimeAsync(2_000)
    expect(fetchDetail).toHaveBeenCalledTimes(1)
  })
})
