// 图片上传 —— 封装 uni.uploadFile，按场景区分上传路径
import { getToken } from '@/services/auth-session'
import { refreshAuthSession } from '@/services/auth-refresh'
import {
  BASE_URL,
  PLACEHOLDER_API_BASE_URL_MESSAGE,
  isPlaceholderApiBaseUrl,
  warnPlaceholderApiBaseUrlOnce,
} from '@/services/request-config'
import { handleErrorCode } from '@/services/request-errors'
import { createApiError, extractApiResponse } from '@/services/request-response'
import type { UploadImageResponse } from '@/types/common'
import { ERROR_CODES, REQUEST_TIMEOUT } from '@/utils/constants'
import { HELP_BACKEND_READY, USE_MOCK } from '@/utils/runtime-env'

interface UploadCallbacks {
  readonly resolve: (value: UploadImageResponse) => void
  readonly reject: (reason?: unknown) => void
}

interface UploadContext {
  readonly filePath: string
  readonly scene: string
  readonly allowAuthRefresh: boolean
  readonly callbacks: UploadCallbacks
}

function buildUploadHeader(token: string): Record<string, string> {
  return token ? { Authorization: `Bearer ${token}` } : {}
}

function shouldRefreshUpload(
  bodyCode: number,
  allowAuthRefresh: boolean,
): boolean {
  return bodyCode === ERROR_CODES.UNAUTHORIZED && allowAuthRefresh && Boolean(getToken())
}

function retryUploadAfterRefresh(context: UploadContext): void {
  const { callbacks, filePath, scene } = context
  refreshAuthSession(REQUEST_TIMEOUT)
    .then(() => uploadFileInternal(filePath, scene, false))
    .then(callbacks.resolve)
    .catch((err) => {
      handleErrorCode(ERROR_CODES.UNAUTHORIZED, '登录已过期，请重新登录')
      callbacks.reject(err)
    })
}

function handleUploadSuccess(
  res: UniApp.UploadFileSuccessCallbackResult,
  context: UploadContext,
): void {
  try {
    const body = extractApiResponse<UploadImageResponse>(JSON.parse(res.data))
    const { callbacks } = context

    if (!body) {
      callbacks.reject(new Error('上传响应解析失败'))
      return
    }

    if (shouldRefreshUpload(body.code, context.allowAuthRefresh)) {
      retryUploadAfterRefresh(context)
      return
    }

    if (res.statusCode < 200 || res.statusCode >= 300) {
      handleErrorCode(body.code, body.message)
      callbacks.reject(createApiError(body.message, body.code, res.statusCode))
      return
    }

    if (body.code !== ERROR_CODES.SUCCESS) {
      handleErrorCode(body.code, body.message)
      callbacks.reject(createApiError(body.message, body.code, res.statusCode))
      return
    }

    callbacks.resolve(body.data)
  } catch {
    context.callbacks.reject(new Error('上传响应解析失败'))
  }
}

function uploadFileInternal(
  filePath: string,
  scene: string,
  allowAuthRefresh: boolean,
): Promise<UploadImageResponse> {
  const token = getToken()

  if (USE_MOCK && !HELP_BACKEND_READY) {
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
      header: buildUploadHeader(token),
      success: (res) => {
        handleUploadSuccess(res, {
          filePath,
          scene,
          allowAuthRefresh,
          callbacks: { resolve, reject },
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
