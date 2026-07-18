// 图片上传 —— 封装 uni.uploadFile，按场景区分上传路径
import {
  captureAuthSession,
  createAuthSessionChangedError,
  isAuthEpochCurrent,
  isAuthSessionCurrent,
  type AuthSessionContext,
} from '@/services/auth-session'
import {
  getRefreshAuthenticationRejection,
  refreshAuthSession,
} from '@/services/auth-refresh'
import {
  BASE_URL,
  PLACEHOLDER_API_BASE_URL_MESSAGE,
  isPlaceholderApiBaseUrl,
  warnPlaceholderApiBaseUrlOnce,
} from '@/services/request-config'
import {
  handleAuthenticationRejection,
  handleErrorCode,
  handleSessionAuthenticationRejection,
} from '@/services/request-errors'
import { createApiError, extractApiResponse } from '@/services/request-response'
import type { ApiResponse, UploadImageResponse } from '@/types/common'
import { ERROR_CODES, REQUEST_TIMEOUT } from '@/utils/constants'
import { HELP_BACKEND_READY, USER_BACKEND_READY, USE_MOCK } from '@/utils/runtime-env'

interface UploadCallbacks {
  readonly resolve: (value: UploadImageResponse) => void
  readonly reject: (reason?: unknown) => void
}

interface UploadContext {
  readonly filePath: string
  readonly scene: string
  readonly allowAuthRefresh: boolean
  readonly callbacks: UploadCallbacks
  readonly auth: AuthSessionContext
}

function buildUploadHeader(token: string): Record<string, string> {
  return token ? { Authorization: `Bearer ${token}` } : {}
}

interface UploadAuthenticationRejection {
  readonly code: number
  readonly message: string
}

function shouldRefreshUpload(
  rejection: UploadAuthenticationRejection | null,
  context: UploadContext,
): boolean {
  return rejection?.code === ERROR_CODES.UNAUTHORIZED
    && context.allowAuthRefresh
    && Boolean(context.auth.token)
    && isAuthEpochCurrent(context.auth)
}

function retryUploadAfterRefresh(context: UploadContext): void {
  const { auth, callbacks, filePath, scene } = context
  const recovery = isAuthSessionCurrent(auth)
    ? refreshAuthSession(REQUEST_TIMEOUT, auth)
    : Promise.resolve()
  recovery
    .then(() => {
      if (!isAuthEpochCurrent(auth)) throw createAuthSessionChangedError()
      return uploadFileInternal(filePath, scene, false)
    })
    .then(callbacks.resolve)
    .catch((err) => {
      const rejection = getRefreshAuthenticationRejection(err)
      if (rejection) {
        handleSessionAuthenticationRejection({
          code: rejection.code,
          message: rejection.message,
          showPrompt: true,
          session: auth,
        })
      }
      callbacks.reject(err)
    })
}

function isUploadBackendReady(scene: string): boolean {
  if (scene === 'avatar' || scene === 'volunteer') return USER_BACKEND_READY
  if (scene === 'help' || scene === 'report') return HELP_BACKEND_READY
  return true
}

function handleUploadSuccess(
  res: UniApp.UploadFileSuccessCallbackResult,
  context: UploadContext,
): void {
  const body = parseUploadBody(res.data)
  const rejection = getUploadAuthenticationRejection(res.statusCode, body)

  if (context.auth.token && !isAuthEpochCurrent(context.auth)) {
    if (rejection) coordinateUploadAuthenticationRejection(rejection, context)
    context.callbacks.reject(createAuthSessionChangedError())
    return
  }
  if (shouldRefreshUpload(rejection, context)) {
    retryUploadAfterRefresh(context)
    return
  }
  if (!body) {
    rejectInvalidUploadResponse(res.statusCode, rejection, context)
    return
  }
  if (res.statusCode < 200 || res.statusCode >= 300 || body.code !== ERROR_CODES.SUCCESS) {
    handleUploadError(body.code, body.message, context.allowAuthRefresh)
    context.callbacks.reject(createApiError(body.message, body.code, res.statusCode))
    return
  }

  context.callbacks.resolve(body.data)
}

function parseUploadBody(data: string): ApiResponse<UploadImageResponse> | null {
  try {
    return extractApiResponse<UploadImageResponse>(JSON.parse(data))
  } catch {
    return null
  }
}

function getUploadAuthenticationRejection(
  statusCode: number,
  body: ApiResponse<UploadImageResponse> | null,
): UploadAuthenticationRejection | null {
  if (body?.code === ERROR_CODES.ACCOUNT_DISABLED) {
    return { code: body.code, message: body.message || '账号已禁用' }
  }
  if (statusCode === 401 || body?.code === ERROR_CODES.UNAUTHORIZED) {
    return { code: ERROR_CODES.UNAUTHORIZED, message: body?.message || '登录已过期，请重新登录' }
  }
  return null
}

function coordinateUploadAuthenticationRejection(
  rejection: UploadAuthenticationRejection,
  context: UploadContext,
): void {
  handleSessionAuthenticationRejection({
    ...rejection,
    showPrompt: true,
    session: context.auth,
  })
}

function rejectInvalidUploadResponse(
  statusCode: number,
  rejection: UploadAuthenticationRejection | null,
  context: UploadContext,
): void {
  if (!rejection) {
    context.callbacks.reject(new Error('上传响应解析失败'))
    return
  }
  coordinateUploadAuthenticationRejection(rejection, context)
  context.callbacks.reject(createApiError(rejection.message, rejection.code, statusCode))
}

function handleUploadError(
  code: number,
  message: string,
  allowAuthRefresh: boolean,
): void {
  if (allowAuthRefresh && code === ERROR_CODES.ACCOUNT_DISABLED) {
    handleAuthenticationRejection(code, message)
    return
  }
  if (shouldHandleUploadError(code, allowAuthRefresh)) {
    handleErrorCode(code, message)
  }
}

function shouldHandleUploadError(code: number, allowAuthRefresh: boolean): boolean {
  if (allowAuthRefresh) return true
  return code !== ERROR_CODES.UNAUTHORIZED && code !== ERROR_CODES.ACCOUNT_DISABLED
}

function uploadFileInternal(
  filePath: string,
  scene: string,
  allowAuthRefresh: boolean,
): Promise<UploadImageResponse> {
  const auth = captureAuthSession()

  if (USE_MOCK && !isUploadBackendReady(scene)) {
    const fileId = `mock-file-${Date.now()}`
    console.log(`[Mock Upload API] ${scene}`, filePath)
    return Promise.resolve({
      url: filePath,
      thumbnail: filePath,
      fileId,
    })
  }

  if (isPlaceholderApiBaseUrl()) {
    warnPlaceholderApiBaseUrlOnce()
    return Promise.reject(new Error(PLACEHOLDER_API_BASE_URL_MESSAGE))
  }

  return new Promise((resolve, reject) => {
    uni.uploadFile({
      url: `${BASE_URL}/api/upload/image`,
      filePath,
      name: 'file',
      formData: { scene },
      header: buildUploadHeader(auth.token),
      success: (res) => {
        handleUploadSuccess(res, {
          filePath,
          scene,
          allowAuthRefresh,
          callbacks: { resolve, reject },
          auth,
        })
      },
      fail: (err) => {
        console.warn('[Upload] uploadFile failed:', err.errMsg)
        reject(new Error(formatUploadFailureMessage(err.errMsg || '')))
      },
    })
  })
}

function formatUploadFailureMessage(errMsg: string): string {
  if (errMsg.includes('url not in domain list')
    || errMsg.includes('domain list')
    || errMsg.includes('合法域名')) {
    return '上传域名未配置'
  }

  if (errMsg.includes('timeout')) return '上传超时'
  if (errMsg.includes('fail')) return '网络上传失败'
  return '文件上传失败'
}

export function uploadFile(
  filePath: string,
  scene: string,
): Promise<UploadImageResponse> {
  return uploadFileInternal(filePath, scene, true)
}
