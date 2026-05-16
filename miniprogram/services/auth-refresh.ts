import { persistAuthSession, getToken } from '@/services/auth-session'
import { AUTH_LOGIN_PATH, AUTH_REFRESH_PATH, BASE_URL } from '@/services/request-config'
import { createApiError, extractApiResponse } from '@/services/request-response'
import type { LoginResponse } from '@/types/common'
import { ERROR_CODES } from '@/utils/constants'

export function canRefreshAuth(url: string, skipAuthRefresh: boolean): boolean {
  if (skipAuthRefresh || url === AUTH_LOGIN_PATH || url === AUTH_REFRESH_PATH) {
    return false
  }

  return Boolean(getToken())
}

export function refreshAuthSession(timeout: number): Promise<void> {
  const token = getToken()
  if (!token) {
    return Promise.reject(createApiError('缺少刷新 Token 所需的登录态', ERROR_CODES.UNAUTHORIZED))
  }

  return new Promise((resolve, reject) => {
    uni.request({
      url: `${BASE_URL}${AUTH_REFRESH_PATH}`,
      method: 'POST',
      header: { Authorization: `Bearer ${token}` },
      timeout,
      success: (res) => {
        const body = extractApiResponse<LoginResponse>(res.data)
        if (!body) {
          reject(createApiError('刷新 Token 响应格式不正确', undefined, res.statusCode))
          return
        }

        if (res.statusCode < 200 || res.statusCode >= 300 || body.code !== ERROR_CODES.SUCCESS) {
          reject(createApiError(body.message, body.code, res.statusCode))
          return
        }

        persistAuthSession(body.data)
        resolve()
      },
      fail: (err) => {
        reject(new Error(err.errMsg || '刷新 Token 请求失败'))
      },
    })
  })
}
