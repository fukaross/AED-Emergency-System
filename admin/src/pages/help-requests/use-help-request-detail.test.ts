import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ElMessage } from 'element-plus'
import { closeHelpRequest, getHelpRequestDetail } from '@/api/help'
import type { AdminHelpDetailResponse } from '@/types/help'
import { useHelpRequestDetail } from './use-help-request-detail'

vi.mock('@/api/help', () => ({
  closeHelpRequest: vi.fn(),
  getHelpRequestDetail: vi.fn(),
}))
vi.mock('@/api/client', () => ({
  getApiErrorMessage: (_error: unknown, fallback: string) => fallback,
}))
vi.mock('@/utils/safe-api-error', () => ({ logApiError: vi.fn() }))
vi.mock('element-plus', () => ({
  ElMessage: { error: vi.fn(), success: vi.fn() },
}))

const getDetailMock = vi.mocked(getHelpRequestDetail)
const closeRequestMock = vi.mocked(closeHelpRequest)

function buildDetail(status: AdminHelpDetailResponse['status'] = 'published'): AdminHelpDetailResponse {
  return {
    id: 'help-1',
    requestNo: 'SOS-1',
    requestChannel: 'user',
    content: '需要帮助',
    type: 'general',
    status,
    address: '教学楼',
    createdAt: '2026-07-18T01:00:00Z',
    closedAt: null,
    closeNote: null,
    reportersCount: 0,
    respondersCount: 0,
    latestEta: null,
    images: [],
    reports: [],
    reporters: [],
    timeline: [],
    responders: [],
  }
}

function response(detail: AdminHelpDetailResponse) {
  return { data: { data: detail } } as Awaited<ReturnType<typeof getHelpRequestDetail>>
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((resolvePromise) => { resolve = resolvePromise })
  return { promise, resolve }
}

beforeEach(() => {
  vi.useFakeTimers()
  vi.clearAllMocks()
  getDetailMock.mockReset()
  closeRequestMock.mockReset()
})

afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

describe('useHelpRequestDetail', () => {
  it('pauses polling while hidden and refreshes immediately when visible again', async () => {
    getDetailMock.mockResolvedValue(response(buildDetail()))
    const state = useHelpRequestDetail({ requestId: 'help-1' })

    await state.initialize()
    await state.setVisible(false)
    await vi.advanceTimersByTimeAsync(20_000)
    expect(getDetailMock).toHaveBeenCalledTimes(1)

    await state.setVisible(true)
    expect(getDetailMock).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(10_000)
    expect(getDetailMock).toHaveBeenCalledTimes(3)
    state.destroy()
  })

  it('waits to load an initially hidden detail until the page becomes visible', async () => {
    getDetailMock.mockResolvedValue(response(buildDetail()))
    const state = useHelpRequestDetail({ requestId: 'help-1', initiallyVisible: false })

    await state.initialize()
    expect(getDetailMock).not.toHaveBeenCalled()
    await state.setVisible(true)

    expect(getDetailMock).toHaveBeenCalledTimes(1)
    expect(state.detail.value?.requestNo).toBe('SOS-1')
    state.destroy()
  })

  it('aborts a hidden initial request and ignores it after the resumed request wins', async () => {
    const hiddenRequest = deferred<Awaited<ReturnType<typeof getHelpRequestDetail>>>()
    const resumedRequest = deferred<Awaited<ReturnType<typeof getHelpRequestDetail>>>()
    getDetailMock
      .mockImplementationOnce(() => hiddenRequest.promise)
      .mockImplementationOnce(() => resumedRequest.promise)
    const state = useHelpRequestDetail({ requestId: 'help-1' })

    const initialization = state.initialize()
    const hiddenSignal = getDetailMock.mock.calls[0]?.[1]
    await state.setVisible(false)
    expect(hiddenSignal?.aborted).toBe(true)
    const resume = state.setVisible(true)
    resumedRequest.resolve(response(buildDetail('responding')))
    await resume
    hiddenRequest.resolve(response(buildDetail('published')))
    await initialization

    expect(state.detail.value?.status).toBe('responding')
    state.destroy()
  })

  it('polls sequentially and exposes stale data after a refresh failure', async () => {
    getDetailMock
      .mockResolvedValueOnce(response(buildDetail()))
      .mockRejectedValueOnce(new Error('refresh failed'))
      .mockResolvedValueOnce(response(buildDetail('responding')))
    const state = useHelpRequestDetail({ requestId: 'help-1' })

    await state.initialize()
    expect(state.detail.value?.status).toBe('published')

    await vi.advanceTimersByTimeAsync(10_000)
    expect(state.detail.value?.status).toBe('published')
    expect(state.refreshErrorMessage.value).toBe('求助详情刷新失败')

    await vi.advanceTimersByTimeAsync(10_000)
    expect(state.detail.value?.status).toBe('responding')
    expect(state.refreshErrorMessage.value).toBe('')
    state.destroy()
  })

  it('retries an initial failure and resumes polling after recovery', async () => {
    getDetailMock
      .mockRejectedValueOnce(new Error('initial failed'))
      .mockResolvedValueOnce(response(buildDetail()))
      .mockResolvedValueOnce(response(buildDetail('responding')))
    const state = useHelpRequestDetail({ requestId: 'help-1' })

    await state.initialize()
    expect(state.loadErrorMessage.value).toBe('获取求助详情数据失败')
    await state.retry()
    expect(state.detail.value?.status).toBe('published')
    await vi.advanceTimersByTimeAsync(10_000)

    expect(state.detail.value?.status).toBe('responding')
    state.destroy()
  })

  it('keeps background retry failures in the error strip after the initial toast', async () => {
    getDetailMock.mockRejectedValue(new Error('unavailable'))
    const state = useHelpRequestDetail({ requestId: 'help-1' })

    await state.initialize()
    await vi.advanceTimersByTimeAsync(20_000)

    expect(getDetailMock).toHaveBeenCalledTimes(3)
    expect(state.loadErrorMessage.value).toBe('求助详情刷新失败')
    expect(ElMessage.error).toHaveBeenCalledTimes(1)
    state.destroy()
  })

  it('does not repeat the initial failure toast across visibility changes', async () => {
    getDetailMock.mockRejectedValue(new Error('unavailable'))
    const state = useHelpRequestDetail({ requestId: 'help-1' })

    await state.initialize()
    await state.setVisible(false)
    await state.setVisible(true)
    await state.setVisible(false)
    await state.setVisible(true)

    expect(getDetailMock).toHaveBeenCalledTimes(3)
    expect(ElMessage.error).toHaveBeenCalledTimes(1)
    state.destroy()
  })

  it('stops polling after a close action returns a terminal detail', async () => {
    getDetailMock
      .mockResolvedValueOnce(response(buildDetail()))
      .mockResolvedValueOnce(response(buildDetail('closed')))
    closeRequestMock.mockResolvedValue({} as Awaited<ReturnType<typeof closeHelpRequest>>)
    const state = useHelpRequestDetail({ requestId: 'help-1' })

    await state.initialize()
    state.handleCloseRequest('closed')
    await state.submitCloseRequest()
    await vi.advanceTimersByTimeAsync(20_000)

    expect(closeRequestMock).toHaveBeenCalledTimes(1)
    expect(getDetailMock).toHaveBeenCalledTimes(2)
    expect(state.detail.value?.status).toBe('closed')
    state.destroy()
  })

  it('aborts and ignores a late initial response after destroy', async () => {
    const pending = deferred<Awaited<ReturnType<typeof getHelpRequestDetail>>>()
    getDetailMock.mockImplementationOnce(() => pending.promise)
    const state = useHelpRequestDetail({ requestId: 'help-1' })

    const initialization = state.initialize()
    const signal = getDetailMock.mock.calls[0]?.[1]
    state.destroy()
    expect(signal?.aborted).toBe(true)
    pending.resolve(response(buildDetail()))
    await initialization

    expect(state.detail.value).toBeNull()
  })

  it('does not revive polling or requests after the detail is destroyed', async () => {
    getDetailMock.mockResolvedValue(response(buildDetail()))
    const state = useHelpRequestDetail({ requestId: 'help-1' })

    state.destroy()
    await state.initialize()
    await vi.advanceTimersByTimeAsync(20_000)

    expect(getDetailMock).not.toHaveBeenCalled()
  })
})
