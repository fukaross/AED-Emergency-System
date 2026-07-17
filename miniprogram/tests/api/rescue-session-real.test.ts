import { beforeEach, describe, expect, it, vi } from 'vitest'

const requestMocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}))

vi.mock('@/utils/runtime-env', () => ({
  APP_ENV: 'production',
  API_BASE_URL: 'REDACTED',
  SESSION_BACKEND_READY: true,
  USER_BACKEND_READY: true,
  USE_MOCK: false,
  USE_REAL_WECHAT_FLOW: true,
  shouldWarnPlaceholderApiBaseUrl: () => false,
}))

vi.mock('@/services/request', () => requestMocks)

import {
  fetchMineRescueSessions,
  fetchRescueSession,
  fetchRescueView,
  joinRescueSession,
  postRescueMessage,
  postRescueStatusEvent,
  reportRescueLocation,
} from '@/api/rescue-session'

describe('rescue-session api (real face)', () => {
  beforeEach(() => {
    requestMocks.get.mockReset()
    requestMocks.post.mockReset()
    requestMocks.put.mockReset()
  })

  it('joins via POST session/join', async () => {
    requestMocks.post.mockResolvedValueOnce({})
    await joinRescueSession('h1')
    expect(requestMocks.post).toHaveBeenCalledWith('/api/help/h1/session/join')
  })

  it('fetches full snapshot without afterSeq', async () => {
    requestMocks.get.mockResolvedValueOnce({})
    await fetchRescueSession('h1')
    expect(requestMocks.get).toHaveBeenCalledWith('/api/help/h1/session', undefined, {
      skipErrorHandler: undefined,
    })
  })

  it('fetches incremental snapshot with afterSeq and silent flag', async () => {
    requestMocks.get.mockResolvedValueOnce({})
    await fetchRescueSession('h1', { afterSeq: 7, silent: true })
    expect(requestMocks.get).toHaveBeenCalledWith(
      '/api/help/h1/session',
      { afterSeq: 7 },
      { skipErrorHandler: true },
    )
  })

  it('posts message text', async () => {
    requestMocks.post.mockResolvedValueOnce({})
    await postRescueMessage('h1', '我到了')
    expect(requestMocks.post).toHaveBeenCalledWith('/api/help/h1/session/messages', {
      text: '我到了',
    })
  })

  it('posts status events', async () => {
    requestMocks.post.mockResolvedValueOnce({})
    await postRescueStatusEvent('h1', 'taken_aed', 'mark')
    expect(requestMocks.post).toHaveBeenCalledWith('/api/help/h1/session/status-events', {
      eventType: 'taken_aed',
      action: 'mark',
    })
  })

  it('reports location with PUT', async () => {
    requestMocks.put.mockResolvedValueOnce({})
    await reportRescueLocation('h1', { lat: 19.9, lng: 110.5 })
    expect(requestMocks.put).toHaveBeenCalledWith('/api/help/h1/session/location', {
      lat: 19.9,
      lng: 110.5,
    })
  })

  it('fetches rescue view', async () => {
    requestMocks.get.mockResolvedValueOnce({})
    await fetchRescueView('h1', { silent: true })
    expect(requestMocks.get).toHaveBeenCalledWith('/api/help/h1/rescue-view', undefined, {
      skipErrorHandler: true,
    })
  })

  it('fetches mine sessions and unwraps items', async () => {
    const items = [{ helpId: 'h1', unreadCount: 2 }]
    requestMocks.get.mockResolvedValueOnce({ items })

    const result = await fetchMineRescueSessions({ silent: true })

    expect(requestMocks.get).toHaveBeenCalledWith('/api/rescue-sessions/mine', undefined, {
      skipErrorHandler: true,
    })
    expect(result).toEqual(items)
  })
})
