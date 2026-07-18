import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@/services/request-config', () => ({
  AUTH_REFRESH_PATH: '/api/auth/refresh',
  BASE_URL: 'https://api.example.test',
  IS_DEV: false,
  PLACEHOLDER_API_BASE_URL_MESSAGE: 'placeholder',
  isPlaceholderApiBaseUrl: () => false,
  warnPlaceholderApiBaseUrlOnce: vi.fn(),
}))

vi.mock('@/utils/runtime-env', () => ({
  HELP_BACKEND_READY: true,
  USER_BACKEND_READY: true,
  USE_MOCK: false,
}))

const storage = new Map<string, unknown>()
const requests: UniApp.RequestOptions[] = []
const uploads: UniApp.UploadFileOption[] = []
let authenticationActive = false
let terminationCount = 0

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key)),
  removeStorageSync: vi.fn((key: string) => storage.delete(key)),
  request: vi.fn((options: UniApp.RequestOptions) => requests.push(options)),
  setStorageSync: vi.fn((key: string, value: unknown) => storage.set(key, value)),
  showModal: vi.fn(),
  showToast: vi.fn(),
  uploadFile: vi.fn((options: UniApp.UploadFileOption) => uploads.push(options)),
})

import { registerAuthenticationTerminator } from '@/services/auth-termination'
import { clearAuthSession, persistAuthSession } from '@/services/auth-session'
import { resetAuthenticationRejectionState } from '@/services/request-errors'
import { get } from '@/services/request'
import { uploadFile } from '@/services/request-upload'
import type { LoginResponse } from '@/types/common'

function buildLogin(token: string): LoginResponse {
  return {
    token,
    expiresIn: 2592000,
    userInfo: { id: 'user-a', name: 'user-a', avatarUrl: '', isNewUser: false },
  }
}

function respondUpload(index: number, statusCode: number, code: number, message: string): void {
  respondUploadRaw(index, statusCode, JSON.stringify({ code, message, data: null }))
}

function respondUploadRaw(index: number, statusCode: number, data: string): void {
  uploads[index]?.success?.({
    statusCode,
    data,
  })
}

function respondUploadSuccess(index: number): void {
  respondUploadRaw(index, 200, JSON.stringify({
    code: 0,
    message: 'success',
    data: { url: 'https://example.test/image.jpg', thumbnail: '', fileId: 'file-1' },
  }))
}

function respondRequest(index: number, statusCode: number, code: number, message: string): void {
  requests[index]?.success?.({
    statusCode,
    data: { code, message, data: null },
    header: {},
    cookies: [],
  })
}

function respondRefresh(): void {
  requests[0]?.success?.({
    statusCode: 200,
    data: {
      code: 0,
      message: 'success',
      data: { token: 'refreshed-token', openId: 'user-a', isNewUser: false },
    },
    header: {},
    cookies: [],
  })
}

async function waitForUploadCount(expected: number): Promise<void> {
  for (let index = 0; index < 10 && uploads.length < expected; index += 1) {
    await Promise.resolve()
  }
  expect(uploads).toHaveLength(expected)
}

describe('upload authentication rejection ownership', () => {
  beforeEach(() => {
    storage.clear()
    requests.length = 0
    uploads.length = 0
    authenticationActive = true
    terminationCount = 0
    vi.clearAllMocks()
    resetAuthenticationRejectionState()
    registerAuthenticationTerminator(() => {
      if (!authenticationActive) return false
      authenticationActive = false
      terminationCount += 1
      clearAuthSession()
      return true
    })
    persistAuthSession(buildLogin('old-token'))
  })

  it('terminates with one server-message modal on an initial 1018 response', async () => {
    const pending = uploadFile('wxfile://photo.jpg', 'help')

    respondUpload(0, 403, 1018, '账号已禁用')

    await expect(pending).rejects.toThrow('账号已禁用')
    expect(requests).toHaveLength(0)
    expect(uni.showModal).toHaveBeenCalledTimes(1)
    expect(uni.showModal).toHaveBeenCalledWith({
      title: '提示',
      content: '账号已禁用',
      showCancel: false,
    })
    expect(terminationCount).toBe(1)
  })

  it('shows one disabled-account modal when the replay returns 1018', async () => {
    const pending = uploadFile('wxfile://photo.jpg', 'help')
    respondUpload(0, 401, 1002, 'unauthorized')
    respondRefresh()
    await waitForUploadCount(2)

    respondUpload(1, 403, 1018, '账号已禁用')

    await expect(pending).rejects.toThrow('账号已禁用')
    expect(uni.showModal).toHaveBeenCalledTimes(1)
    expect(uni.showModal).toHaveBeenCalledWith({
      title: '提示',
      content: '账号已禁用',
      showCancel: false,
    })
    expect(terminationCount).toBe(1)
  })

  it('shows one login-expired modal when the replay returns 401', async () => {
    const pending = uploadFile('wxfile://photo.jpg', 'help')
    respondUpload(0, 401, 1002, 'unauthorized')
    respondRefresh()
    await waitForUploadCount(2)

    respondUpload(1, 401, 1002, 'token expired')

    await expect(pending).rejects.toThrow('token expired')
    expect(uni.showModal).toHaveBeenCalledTimes(1)
    expect(uni.showModal).toHaveBeenCalledWith({
      title: '登录过期',
      content: '请重新登录',
      showCancel: false,
    })
    expect(terminationCount).toBe(1)
  })

  it('keeps authentication for a business forbidden upload response', async () => {
    const pending = uploadFile('wxfile://photo.jpg', 'help')

    respondUpload(0, 403, 1003, '仅认证急救员可上传')

    await expect(pending).rejects.toThrow('仅认证急救员可上传')
    expect(terminationCount).toBe(0)
    expect(uni.showModal).toHaveBeenCalledWith({
      title: '提示',
      content: '没有权限执行此操作',
      showCancel: false,
    })
  })

  it('coalesces concurrent replay 401 responses into one modal', async () => {
    const first = uploadFile('wxfile://first.jpg', 'help')
    const second = uploadFile('wxfile://second.jpg', 'help')
    respondUpload(0, 401, 1002, 'unauthorized')
    respondUpload(1, 401, 1002, 'unauthorized')
    respondRefresh()
    await waitForUploadCount(4)

    respondUpload(2, 401, 1002, 'token expired')
    respondUpload(3, 401, 1002, 'token expired')
    await Promise.allSettled([first, second])

    expect(uni.showModal).toHaveBeenCalledTimes(1)
    expect(terminationCount).toBe(1)
  })

  it('lets a visible upload own the prompt after a silent JSON rejection', async () => {
    const silent = get('/api/silent', undefined, { skipErrorHandler: true })
    const visible = uploadFile('wxfile://visible.jpg', 'help')

    respondRequest(0, 403, 1018, '账号已禁用')
    respondUpload(0, 403, 1018, '账号已禁用')
    await Promise.allSettled([silent, visible])

    expect(terminationCount).toBe(1)
    expect(uni.showModal).toHaveBeenCalledTimes(1)
    expect(uni.showModal).toHaveBeenCalledWith({
      title: '提示',
      content: '账号已禁用',
      showCancel: false,
    })
  })

  it('refreshes and replays an initial bare HTTP 401 upload', async () => {
    const pending = uploadFile('wxfile://photo.jpg', 'help')
    respondUploadRaw(0, 401, '<html>unauthorized</html>')
    respondRefresh()
    await waitForUploadCount(2)
    respondUploadSuccess(1)

    await expect(pending).resolves.toMatchObject({ fileId: 'file-1' })
    expect(terminationCount).toBe(0)
    expect(uni.showModal).not.toHaveBeenCalled()
  })

  it('terminates once when the upload replay returns a bare HTTP 401', async () => {
    const pending = uploadFile('wxfile://photo.jpg', 'help')
    respondUpload(0, 401, 1002, 'unauthorized')
    respondRefresh()
    await waitForUploadCount(2)
    respondUploadRaw(1, 401, 'gateway rejected the request')

    await expect(pending).rejects.toThrow('登录已过期')
    expect(terminationCount).toBe(1)
    expect(uni.showModal).toHaveBeenCalledTimes(1)
    expect(uni.showModal).toHaveBeenCalledWith({
      title: '登录过期',
      content: '请重新登录',
      showCancel: false,
    })
  })
})
