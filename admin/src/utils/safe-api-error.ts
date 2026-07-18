import { getApiErrorCode, getApiErrorMessage, getApiErrorStatus } from '@/api/client'

export interface SafeApiErrorLog {
  readonly context: string
  readonly status?: number
  readonly code?: string | number
  readonly message: string
}

interface SafeApiErrorOptions {
  readonly context: string
  readonly error: unknown
  readonly fallback: string
}

/** 只保留可公开诊断字段，绝不传递 Axios config、headers 或响应正文。 */
export function toSafeApiErrorLog(options: SafeApiErrorOptions): SafeApiErrorLog {
  return {
    context: options.context,
    status: getApiErrorStatus(options.error),
    code: getApiErrorCode(options.error),
    message: getApiErrorMessage(options.error, options.fallback),
  }
}

/** 将 API 失败写入脱敏浏览器日志。 */
export function logApiError(options: SafeApiErrorOptions): void {
  console.error('API request failed', toSafeApiErrorLog(options))
}
