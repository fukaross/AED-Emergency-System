/**
 * 统一网络请求基座
 *
 * [G-05] 页面禁止直调 uni.request，所有请求必须通过本模块发起。
 * [G-07] API 基地址不含 /api，API 模块使用完整路径如 /api/aed/nearby。
 *
 * 功能：
 * - 自动注入 Authorization header (Bearer token)
 * - 统一超时 (默认 10s)
 * - 错误码映射 (BackEnd.md §4.1)
 * - 开发环境请求/响应日志
 * - 错误分级提示 (Toast / Modal)
 */
import { ERROR_CODES, ERROR_MESSAGES, REQUEST_TIMEOUT, TOKEN_STORAGE_KEY } from '@/utils/constants'
import type { ApiResponse } from '@/types/common'
import {
  API_BASE_URL,
  IS_PLACEHOLDER_API_BASE_URL,
  shouldWarnPlaceholderApiBaseUrl,
} from '@/utils/runtime-env'

// ===== 基地址 =====
const BASE_URL = API_BASE_URL
const IS_DEV = import.meta.env.VITE_APP_ENV === 'development'
const PLACEHOLDER_API_BASE_URL_MESSAGE =
  '当前接口基址仍是占位地址 api.example.com，请切换到开发/预览构建，或配置真实后端地址。'
let hasWarnedPlaceholderApiBaseUrl = false

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
  readonly timeout?: number
}

// ===== 令牌读取 =====
function getToken(): string {
  try {
    return (uni.getStorageSync(TOKEN_STORAGE_KEY) as string) || ''
  } catch {
    return ''
  }
}

// ===== 日志 =====
function logRequest(method: string, url: string, data?: unknown): void {
  if (!IS_DEV) return
  console.log(`[Request] ${method} ${url}`, data ?? '')
}

function logResponse(url: string, res: unknown): void {
  if (!IS_DEV) return
  console.log(`[Response] ${url}`, res)
}

function warnPlaceholderApiBaseUrlOnce(): void {
  if (!shouldWarnPlaceholderApiBaseUrl() || hasWarnedPlaceholderApiBaseUrl) {
    return
  }

  console.warn(`[Request] ${PLACEHOLDER_API_BASE_URL_MESSAGE}`)
  hasWarnedPlaceholderApiBaseUrl = true
}

// ===== 错误提示分级 =====
function handleErrorCode(code: number, message: string): void {
  if (code === ERROR_CODES.UNAUTHORIZED) {
    uni.showModal({
      title: '登录过期',
      content: '请重新登录',
      showCancel: false,
      success: () => {
        uni.removeStorageSync(TOKEN_STORAGE_KEY)
      },
    })
    return
  }

  const userMessage = ERROR_MESSAGES[code] || message || '请求失败'

  const modalCodes: readonly number[] = [
    ERROR_CODES.FORBIDDEN,
    ERROR_CODES.INTERNAL_ERROR,
  ]

  if (modalCodes.includes(code)) {
    uni.showModal({ title: '提示', content: userMessage, showCancel: false })
  } else {
    uni.showToast({ title: userMessage, icon: 'none', duration: 2000 })
  }
}

// ===== 核心请求函数 =====
export function request<T = unknown>(options: RequestOptions): Promise<T> {
  const {
    url,
    method = 'GET',
    data,
    header = {},
    skipErrorHandler = false,
    timeout = REQUEST_TIMEOUT,
  } = options

  const fullUrl = `${BASE_URL}${url}`

  const token = getToken()
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...header,
  }
  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }

  logRequest(method, fullUrl, data)

  if (IS_PLACEHOLDER_API_BASE_URL) {
    warnPlaceholderApiBaseUrlOnce()
    if (!skipErrorHandler) {
      uni.showToast({ title: '接口地址未配置', icon: 'none' })
    }
    return Promise.reject(new Error(PLACEHOLDER_API_BASE_URL_MESSAGE))
  }

  return new Promise<T>((resolve, reject) => {
    uni.request({
      url: fullUrl,
      method,
      data,
      header: headers,
      timeout,
      success: (res) => {
        logResponse(url, res.data)

        const statusCode = res.statusCode
        if (statusCode < 200 || statusCode >= 300) {
          const errMsg = `HTTP ${statusCode}`
          if (!skipErrorHandler) {
            uni.showToast({ title: `网络错误 (${statusCode})`, icon: 'none' })
          }
          reject(new Error(errMsg))
          return
        }

        const body = res.data as ApiResponse<T>

        if (body.code !== ERROR_CODES.SUCCESS) {
          if (!skipErrorHandler) {
            handleErrorCode(body.code, body.message)
          }
          reject(Object.assign(new Error(body.message), { code: body.code }))
          return
        }

        resolve(body.data)
      },
      fail: (err) => {
        console.error(`[Request] fail ${url}`, err)
        if (!skipErrorHandler) {
          uni.showToast({ title: '网络连接失败，请检查网络', icon: 'none' })
        }
        reject(new Error(err.errMsg || '网络请求失败'))
      },
    })
  })
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

// ===== 文件上传 =====
export function uploadFile(
  filePath: string,
  scene: string,
): Promise<{ url: string; thumbnail: string; fileId: string }> {
  const token = getToken()

  if (IS_PLACEHOLDER_API_BASE_URL) {
    warnPlaceholderApiBaseUrlOnce()
    return Promise.reject(new Error(PLACEHOLDER_API_BASE_URL_MESSAGE))
  }

  return new Promise((resolve, reject) => {
    uni.uploadFile({
      url: `${BASE_URL}/api/upload/image`,
      filePath,
      name: 'file',
      formData: { scene },
      header: token ? { Authorization: `Bearer ${token}` } : {},
      success: (res) => {
        if (res.statusCode !== 200) {
          reject(new Error(`Upload HTTP ${res.statusCode}`))
          return
        }
        try {
          const body = JSON.parse(res.data) as ApiResponse<{
            url: string
            thumbnail: string
            fileId: string
          }>
          if (body.code !== ERROR_CODES.SUCCESS) {
            handleErrorCode(body.code, body.message)
            reject(new Error(body.message))
            return
          }
          resolve(body.data)
        } catch {
          reject(new Error('上传响应解析失败'))
        }
      },
      fail: (err) => {
        reject(new Error(err.errMsg || '文件上传失败'))
      },
    })
  })
}
