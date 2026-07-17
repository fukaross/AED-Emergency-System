// 统一网络请求 —— 自动带 token、超时 10s、错误分级提示。页面别自己调 uni.request
import {
  ERROR_CODES,
  REQUEST_TIMEOUT,
} from '@/utils/constants'
import { getToken } from '@/services/auth-session'
import { canRefreshAuth, refreshAuthSession } from '@/services/auth-refresh'
import {
  BASE_URL,
  IS_DEV,
  PLACEHOLDER_API_BASE_URL_MESSAGE,
  isPlaceholderApiBaseUrl,
  warnPlaceholderApiBaseUrlOnce,
} from '@/services/request-config'
import { handleErrorCode } from '@/services/request-errors'
import { createApiError, extractApiResponse } from '@/services/request-response'
import type { ApiResponse } from '@/types/common'

// ===== 类型 =====
type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE'

interface RequestOptions {
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
}

// ===== 日志 =====
function sanitizeLogPath(url: string): string {
  return url.split('?')[0]
}

function logRequest(method: string, url: string): void {
  if (!IS_DEV) return
  console.log(`[Request] ${method} ${sanitizeLogPath(url)}`)
}

function logResponse(
  url: string,
  statusCode: number,
  apiCode: number | undefined,
): void {
  if (!IS_DEV) return
  const codeText = apiCode === undefined ? 'invalid' : String(apiCode)
  console.log(
    `[Response] ${sanitizeLogPath(url)} status=${statusCode} code=${codeText}`,
  )
}

function retryAfterRefresh<T>(
  options: RequestOptions,
  resolve: (value: T) => void,
  reject: (reason?: unknown) => void,
): void {
  refreshAuthSession(options.timeout ?? REQUEST_TIMEOUT)
    .then(() => request<T>({ ...options, skipAuthRefresh: true }))
    .then(resolve)
    .catch((err) => {
      if (!options.skipErrorHandler) {
        handleErrorCode(ERROR_CODES.UNAUTHORIZED, '登录已过期，请重新登录')
      }
      reject(err)
    })
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
  const { body, callbacks, options, url } = context
  if (body?.code !== ERROR_CODES.UNAUTHORIZED) {
    return false
  }

  if (!canRefreshAuth(url, options.skipAuthRefresh ?? false)) {
    return false
  }

  retryAfterRefresh(options, callbacks.resolve, callbacks.reject)
  return true
}

function rejectBodyError<T>(context: ResponseContext<T>): void {
  const { body, callbacks, options, statusCode } = context
  if (!body) {
    return
  }

  if (!options.skipErrorHandler) {
    handleErrorCode(body.code, body.message)
  }

  callbacks.reject(createApiError(body.message, body.code, statusCode))
}

function rejectHttpError<T>(context: ResponseContext<T>): void {
  const { callbacks, options, statusCode } = context
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
  const { body, callbacks, statusCode } = context

  if (statusCode < 200 || statusCode >= 300) {
    if (!body) {
      rejectHttpError(context)
      return
    }

    if (!tryRefresh(context)) {
      rejectBodyError(context)
    }
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

function rejectPlaceholderRequest(skipErrorHandler: boolean): Promise<never> {
  warnPlaceholderApiBaseUrlOnce()
  if (!skipErrorHandler) {
    uni.showToast({ title: '接口地址未配置', icon: 'none' })
  }

  return Promise.reject(new Error(PLACEHOLDER_API_BASE_URL_MESSAGE))
}

function sendUniRequest<T>(
  options: RequestOptions,
  fullUrl: string,
  headers: Record<string, string>,
): Promise<T> {
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
        })
      },
      fail: (err) => {
        console.error(`[Request] ${method} ${sanitizeLogPath(url)} network-failure`)
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

  const fullUrl = `${BASE_URL}${url}`

  const token = getToken()
  const headers = buildHeaders(header, token)

  logRequest(method, url)

  if (isPlaceholderApiBaseUrl()) {
    return rejectPlaceholderRequest(skipErrorHandler)
  }

  return sendUniRequest<T>(
    { ...options, method, data, skipAuthRefresh, skipErrorHandler, timeout },
    fullUrl,
    headers,
  )
}

// ===== 便捷方法 =====

export function get<T = unknown>(
  url: string,
  data?: Record<string, unknown>,
  options?: Partial<RequestOptions>,
): Promise<T> {
  return request<T>({ url, method: 'GET', data, ...options })
}

export function post<T = unknown>(
  url: string,
  data?: Record<string, unknown>,
  options?: Partial<RequestOptions>,
): Promise<T> {
  return request<T>({ url, method: 'POST', data, ...options })
}

export function put<T = unknown>(
  url: string,
  data?: Record<string, unknown>,
  options?: Partial<RequestOptions>,
): Promise<T> {
  return request<T>({ url, method: 'PUT', data, ...options })
}

export function del<T = unknown>(
  url: string,
  data?: Record<string, unknown>,
  options?: Partial<RequestOptions>,
): Promise<T> {
  return request<T>({ url, method: 'DELETE', data, ...options })
}

