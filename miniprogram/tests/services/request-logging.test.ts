import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@/services/request-config', () => ({
  BASE_URL: 'https://api.example.test',
  IS_DEV: true,
  PLACEHOLDER_API_BASE_URL_MESSAGE: 'placeholder',
  isPlaceholderApiBaseUrl: () => false,
  warnPlaceholderApiBaseUrlOnce: vi.fn(),
}))

vi.mock('@/services/auth-session', () => ({
  getToken: () => 'secret-token-value',
}))

vi.mock('@/services/auth-refresh', () => ({
  canRefreshAuth: () => false,
  refreshAuthSession: vi.fn(),
}))

vi.mock('@/services/request-errors', () => ({ handleErrorCode: vi.fn() }))

import { put } from '@/services/request'

describe('request development logging', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('logs only method, path, status and API code', async () => {
    const log = vi.spyOn(console, 'log').mockImplementation(() => undefined)
    vi.stubGlobal('uni', {
      request: vi.fn((options) => options.success({
        statusCode: 200,
        data: {
          code: 0,
          message: 'ok',
          data: { reportedAt: '2026-07-18T10:00:00.000Z', lat: 19.97, lng: 110.51 },
        },
      })),
    })

    await put('/api/help/h1/session/location?debug=secret', {
      lat: 19.97,
      lng: 110.51,
    })

    expect(log).toHaveBeenCalledWith('[Request] PUT /api/help/h1/session/location')
    expect(log).toHaveBeenCalledWith(
      '[Response] /api/help/h1/session/location status=200 code=0',
    )
    const serialized = JSON.stringify(log.mock.calls)
    expect(serialized).not.toContain('19.97')
    expect(serialized).not.toContain('110.51')
    expect(serialized).not.toContain('secret')
    expect(serialized).not.toContain('reportedAt')
  })
})
