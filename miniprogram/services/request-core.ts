// 统一网络请求：自动带 token、超时 10s、错误分级提示。页面别自己调 uni.request。
import { ERROR_CODES, REQUEST_TIMEOUT } from '@/utils/constants'
import {
  captureAuthSession, createAuthSessionChangedError, isAuthEpochCurrent,
  isAuthSessionCurrent, type AuthSessionContext,
} from '@/services/auth-session'
import { canRefreshAuth, getRefreshAuthenticationRejection, refreshAuthSession } from '@/services/auth-refresh'
import {
  BASE_URL,
  PLACEHOLDER_API_BASE_URL_MESSAGE,
  isPlaceholderApiBaseUrl,
  warnPlaceholderApiBaseUrlOnce,
} from '@/services/request-config'
import { handleErrorCode, handleSessionAuthenticationRejection } from '@/services/request-errors'
import { logNetworkFailure, logRequest, logResponse } from '@/services/request-logging'
import { createApiError, extractApiResponse } from '@/services/request-response'
import type { ApiResponse } from '@/types/common'

// ===== 类型 =====
type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE'

export interface RequestOptions {
  /** 请求路径（完整路径如 /api/aed/nearby） */
  readonly url: string
  readonly method?: HttpMethod
  readonly data?: Record<string, unknown>
  readonly header?: Record<string, string>
  /** 是否跳过自动错误处理 */
  readonly skipErrorHandler?: boolean
  readonly skipAuthRefresh?: boolean
  readonly timeout?: number
}

interface RequestCallbacks<T> {
  readonly resolve: (value: T) => void
  readonly reject: (reason?: unknown) => void
}

interface ResponseContext<T> {
  readonly url: string
  readonly statusCode: number
  readonly body: ApiResponse<T> | null
  readonly options: RequestOptions
  readonly callbacks: RequestCallbacks<T>
  readonly auth: AuthSessionContext
}

interface UniRequestContext {
  readonly options: RequestOptions
  readonly fullUrl: string
  readonly headers: Record<string, string>
  readonly auth: AuthSessionContext
}

function retryAfterRefresh<T>(context: ResponseContext<T>): void {
  const { auth, callbacks, options } = context
  recoverAuthentication(context).then(
    () => replayRequest(context).then(callbacks.resolve, callbacks.reject),
    (err) => {
      const rejection = getRefreshAuthenticationRejection(err)
      if (rejection) {
        handleSessionAuthenticationRejection({
          code: rejection.code,
          message: rejection.message,
          showPrompt: !options.skipErrorHandler,
          session: auth,
        })
      }
      callbacks.reject(err)
    },
  )
}

function replayRequest<T>(context: ResponseContext<T>): Promise<T> {
  if (!isAuthEpochCurrent(context.auth)) {
    return Promise.reject(createAuthSessionChangedError())
  }
  return request<T>({ ...context.options, skipAuthRefresh: true })
}

function recoverAuthentication<T>(context: ResponseContext<T>): Promise<void> {
  const { auth, options } = context
  if (!isAuthEpochCurrent(auth)) {
    return Promise.reject(createAuthSessionChangedError())
  }
  if (!isAuthSessionCurrent(auth)) return Promise.resolve()
  return refreshAuthSession(options.timeout ?? REQUEST_TIMEOUT, auth)
}

function buildHeaders(
  header: Record<string, string>,
  token: string,
): Record<string, string> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...header,
  }

  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }

  return headers
}

function tryRefresh<T>(context: ResponseContext<T>): boolean {
  const { body, options, statusCode, url } = context
  if (statusCode !== 401 && body?.code !== ERROR_CODES.UNAUTHORIZED) {
    return false
  }

  if (!canRefreshAuth(url, options.skipAuthRefresh ?? false, context.auth)) {
    return false
  }

  retryAfterRefresh(context)
  return true
}

function rejectBodyError<T>(context: ResponseContext<T>): void {
  const { body, callbacks, statusCode } = context
  if (!body) {
    return
  }

  handleRequestError(context, body.code, body.message)

  callbacks.reject(createApiError(body.message, body.code, statusCode))
}

function rejectHttpError<T>(context: ResponseContext<T>): void {
  const { callbacks, options, statusCode } = context
  if (statusCode === 401) {
    const message = '登录已过期，请重新登录'
    handleRequestError(context, ERROR_CODES.UNAUTHORIZED, message)
    callbacks.reject(createApiError(message, ERROR_CODES.UNAUTHORIZED, statusCode))
    return
  }
  if (!options.skipErrorHandler) {
    uni.showToast({ title: `网络错误 (${statusCode})`, icon: 'none' })
  }

  callbacks.reject(createApiError(`HTTP ${statusCode}`, undefined, statusCode))
}

function rejectInvalidBody<T>(context: ResponseContext<T>): void {
  const { callbacks, options, statusCode } = context
  const message = '接口响应格式不正确'
  if (!options.skipErrorHandler) {
    uni.showToast({ title: message, icon: 'none' })
  }

  callbacks.reject(createApiError(message, undefined, statusCode))
}

function handleResponse<T>(context: ResponseContext<T>): void {
  const { auth, body, callbacks, statusCode } = context

  if (auth.token && !isAuthEpochCurrent(auth)) {
    handleStaleAuthenticationRejection(context)
    callbacks.reject(createAuthSessionChangedError())
    return
  }

  if (statusCode < 200 || statusCode >= 300) {
    if (tryRefresh(context)) return
    if (!body) {
      rejectHttpError(context)
      return
    }

    rejectBodyError(context)
    return
  }

  if (!body) {
    rejectInvalidBody(context)
    return
  }

  if (body.code !== ERROR_CODES.SUCCESS) {
    if (!tryRefresh(context)) {
      rejectBodyError(context)
    }
    return
  }

  callbacks.resolve(body.data)
}

function handleRequestError<T>(
  context: ResponseContext<T>,
  code: number,
  message: string,
): void {
  const { auth, options } = context
  if (code === ERROR_CODES.UNAUTHORIZED || code === ERROR_CODES.ACCOUNT_DISABLED) {
    handleSessionAuthenticationRejection({
      code,
      message,
      showPrompt: !options.skipErrorHandler,
      session: auth,
    })
    return
  }
  if (!options.skipErrorHandler) {
    handleErrorCode(code, message)
  }
}

function handleStaleAuthenticationRejection<T>(context: ResponseContext<T>): void {
  const { body, statusCode } = context
  if (body?.code === ERROR_CODES.UNAUTHORIZED
    || body?.code === ERROR_CODES.ACCOUNT_DISABLED) {
    handleRequestError(context, body.code, body.message)
    return
  }
  if (statusCode === 401) {
    handleRequestError(context, ERROR_CODES.UNAUTHORIZED, '登录已过期，请重新登录')
  }
}

function rejectPlaceholderRequest(skipErrorHandler: boolean): Promise<never> {
  warnPlaceholderApiBaseUrlOnce()
  if (!skipErrorHandler) {
    uni.showToast({ title: '接口地址未配置', icon: 'none' })
  }

  return Promise.reject(new Error(PLACEHOLDER_API_BASE_URL_MESSAGE))
}

function sendUniRequest<T>(context: UniRequestContext): Promise<T> {
  const { auth, fullUrl, headers, options } = context
  const {
    data,
    method = 'GET',
    skipAuthRefresh = false,
    skipErrorHandler = false,
    timeout = REQUEST_TIMEOUT,
    url,
  } = options

  return new Promise<T>((resolve, reject) => {
    uni.request({
      url: fullUrl,
      method,
      data,
      header: headers,
      timeout,
      success: (res) => {
        const body = extractApiResponse<T>(res.data)
        logResponse(url, res.statusCode, body?.code)

        handleResponse({
          url,
          statusCode: res.statusCode,
          body,
          options: { ...options, skipErrorHandler, skipAuthRefresh },
          callbacks: { resolve, reject },
          auth,
        })
      },
      fail: (err) => {
        logNetworkFailure(method, url)
        if (!skipErrorHandler) {
          uni.showToast({ title: '网络连接失败，请检查网络', icon: 'none' })
        }
        reject(new Error(err.errMsg || '网络请求失败'))
      },
    })
  })
}

// ===== 核心请求函数 =====
export function request<T = unknown>(options: RequestOptions): Promise<T> {
  const {
    url,
    method = 'GET',
    data,
    header = {},
    skipErrorHandler = false,
    skipAuthRefresh = false,
    timeout = REQUEST_TIMEOUT,
  } = options
  const auth = captureAuthSession()
  const fullUrl = `${BASE_URL}${url}`
  const headers = buildHeaders(header, auth.token)
  logRequest(method, url)

  if (isPlaceholderApiBaseUrl()) {
    return rejectPlaceholderRequest(skipErrorHandler)
  }

  return sendUniRequest<T>({
    options: { ...options, method, data, skipAuthRefresh, skipErrorHandler, timeout },
    fullUrl,
    headers,
    auth,
  })
}
