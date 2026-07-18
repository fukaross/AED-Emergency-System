import { describe, expect, it, vi } from 'vitest'
import { logApiError, toSafeApiErrorLog } from './safe-api-error'

const SECRET = 'admin-session-secret'

vi.mock('@/api/client', () => ({
  getApiErrorStatus: (error: { response?: { status?: number } }) => error.response?.status,
  getApiErrorCode: (error: { response?: { data?: { code?: string } } }) => error.response?.data?.code,
  getApiErrorMessage: (
    error: { response?: { data?: { message?: string } } },
    fallback: string,
  ) => error.response?.data?.message ?? fallback,
}))

function axiosErrorWithSecret() {
  return {
    isAxiosError: true,
    message: 'Request failed',
    config: { headers: { 'X-Aed-Admin-Key': SECRET } },
    response: {
      status: 503,
      data: { code: 'TEMPORARY_FAILURE', message: '服务暂时不可用' },
    },
  }
}

describe('safe API error logging', () => {
  it('keeps only status, code, message and context', () => {
    expect(toSafeApiErrorLog({
      context: 'rescue-session-list.background',
      error: axiosErrorWithSecret(),
      fallback: '加载失败',
    })).toEqual({
      context: 'rescue-session-list.background',
      status: 503,
      code: 'TEMPORARY_FAILURE',
      message: '服务暂时不可用',
    })
  })

  it('never forwards Axios config or headers to console', () => {
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    logApiError({ context: 'test', error: axiosErrorWithSecret(), fallback: '加载失败' })

    const serializedLog = JSON.stringify(consoleSpy.mock.calls)
    expect(serializedLog).not.toContain(SECRET)
    expect(serializedLog).not.toContain('X-Aed-Admin-Key')
    expect(serializedLog).not.toContain('config')
    consoleSpy.mockRestore()
  })
})
