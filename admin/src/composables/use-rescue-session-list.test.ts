import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { AdminRescueSessionListResponse } from '@/types/rescue-session'
import { useRescueSessionList, type RescueSessionListQuery } from './use-rescue-session-list'

vi.mock('@/api/rescue-sessions', () => ({ getRescueSessions: vi.fn() }))
vi.mock('@/api/client', () => ({
  getApiErrorMessage: (_error: unknown, fallback: string) => fallback,
}))
vi.mock('@/utils/safe-api-error', () => ({ logApiError: vi.fn() }))

const activeQuery = { status: 'active', page: 1, pageSize: 10 } as const

function buildResponse(requestNo = 'SOS-NEW'): AdminRescueSessionListResponse {
  return {
    items: [{
      helpId: `help-${requestNo}`,
      requestNo,
      helpStatus: 'published',
      type: 'general',
      address: '教学楼',
      createdAt: '2026-07-18T01:00:00Z',
      participantsCount: 1,
      messagesCount: 2,
      lastActivityAt: '2026-07-18T01:01:00Z',
    }],
    total: 1,
    page: 1,
    pageSize: 10,
    totalPages: 1,
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason?: unknown) => void
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise
    reject = rejectPromise
  })
  return { promise, resolve, reject }
}

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

describe('useRescueSessionList visibility lifecycle', () => {
  it('does not request while initially hidden and refreshes immediately on resume', async () => {
    const fetchList = vi.fn().mockResolvedValue(buildResponse())
    const state = useRescueSessionList({
      query: activeQuery,
      fetchList,
      initiallyVisible: false,
      pollIntervalMs: 1_000,
    })

    await state.initialize()
    await vi.advanceTimersByTimeAsync(2_000)
    expect(fetchList).not.toHaveBeenCalled()

    await state.setVisible(true)
    expect(fetchList).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(1_000)
    expect(fetchList).toHaveBeenCalledTimes(2)
    state.destroy()
  })

  it('aborts a hidden request and ignores its late result', async () => {
    const pending = deferred<AdminRescueSessionListResponse>()
    const signals: AbortSignal[] = []
    const fetchList = vi.fn((_query: RescueSessionListQuery, signal: AbortSignal) => {
      signals.push(signal)
      return pending.promise
    })
    const state = useRescueSessionList({ query: activeQuery, fetchList })

    const initialization = state.initialize()
    await state.setVisible(false)
    expect(signals[0].aborted).toBe(true)
    pending.resolve(buildResponse('SOS-LATE'))
    await initialization

    expect(state.hasLoadedSuccessfully.value).toBe(false)
    expect(state.items.value).toEqual([])
    state.destroy()
  })
})

describe('useRescueSessionList request ordering', () => {
  it('aborts and ignores the previous query when tab or page changes', async () => {
    const oldRequest = deferred<AdminRescueSessionListResponse>()
    const newRequest = deferred<AdminRescueSessionListResponse>()
    const signals: AbortSignal[] = []
    const fetchList = vi.fn((query: RescueSessionListQuery, signal: AbortSignal) => {
      signals.push(signal)
      return query.status === 'active' ? oldRequest.promise : newRequest.promise
    })
    const state = useRescueSessionList({ query: activeQuery, fetchList })

    const oldLoad = state.initialize()
    const switchLoad = state.setQuery({ ...activeQuery, status: 'history' })
    expect(signals[0].aborted).toBe(true)
    newRequest.resolve(buildResponse('SOS-HISTORY'))
    await switchLoad
    oldRequest.resolve(buildResponse('SOS-ACTIVE'))
    await oldLoad

    expect(state.items.value[0]?.requestNo).toBe('SOS-HISTORY')
    state.destroy()
  })

  it('schedules the next poll only after the current request completes', async () => {
    const pollRequest = deferred<AdminRescueSessionListResponse>()
    const fetchList = vi.fn()
      .mockResolvedValueOnce(buildResponse())
      .mockImplementationOnce(() => pollRequest.promise)
      .mockResolvedValue(buildResponse())
    const state = useRescueSessionList({
      query: activeQuery,
      fetchList,
      pollIntervalMs: 1_000,
    })

    await state.initialize()
    await vi.advanceTimersByTimeAsync(1_000)
    expect(fetchList).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(5_000)
    expect(fetchList).toHaveBeenCalledTimes(2)

    pollRequest.resolve(buildResponse())
    await vi.advanceTimersByTimeAsync(999)
    expect(fetchList).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(1)
    expect(fetchList).toHaveBeenCalledTimes(3)
    state.destroy()
  })
})

describe('useRescueSessionList error state', () => {
  it('keeps first errors persistent and marks retained data stale on refresh failure', async () => {
    const fetchList = vi.fn()
      .mockRejectedValueOnce(new Error('initial failed'))
      .mockResolvedValueOnce(buildResponse('SOS-GOOD'))
      .mockRejectedValueOnce(new Error('refresh failed'))
      .mockResolvedValueOnce(buildResponse('SOS-RECOVERED'))
    const state = useRescueSessionList({
      query: activeQuery,
      fetchList,
      pollIntervalMs: 60_000,
      now: () => '2026-07-18T01:02:00Z',
    })

    await state.initialize()
    expect(state.hasLoadedSuccessfully.value).toBe(false)
    expect(state.errorMessage.value).toBe('加载急救会话列表失败')

    await state.retry()
    expect(state.hasLoadedSuccessfully.value).toBe(true)
    expect(state.lastSuccessfulAt.value).toBe('2026-07-18T01:02:00Z')
    await state.setVisible(true)
    expect(state.items.value[0]?.requestNo).toBe('SOS-GOOD')
    expect(state.refreshErrorMessage.value).toBe('加载急救会话列表失败')

    await state.setVisible(true)
    expect(state.items.value[0]?.requestNo).toBe('SOS-RECOVERED')
    expect(state.refreshErrorMessage.value).toBe('')
    state.destroy()
  })
})
