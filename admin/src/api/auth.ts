import client from './client'
import type {
  AdminLoginRequest,
  AdminLoginResponse,
  AdminBootstrapRequest,
  AdminResetSuperAdminRequest,
  AdminMeResponse,
  AdminSessionListItem,
} from '@/types/auth'
import type { ApiResponse } from '@/types/api'

interface LegacyAdminKeyOptions {
  adminKey: string
}

const ADMIN_DEVICE_ID_KEY = 'adminDeviceId'

function legacyAdminKeyHeaders(options: LegacyAdminKeyOptions) {
  return { 'X-Aed-Admin-Key': options.adminKey }
}

function createDeviceId() {
  const cryptoApi = globalThis.crypto
  if (!cryptoApi) {
    throw new Error('当前浏览器缺少 crypto API，无法生成管理端设备标识')
  }

  if (cryptoApi.randomUUID) {
    return cryptoApi.randomUUID()
  }

  const values = new Uint32Array(4)
  cryptoApi.getRandomValues(values)
  return Array.from(values, value => value.toString(16).padStart(8, '0')).join('')
}

function getDeviceId() {
  const current = localStorage.getItem(ADMIN_DEVICE_ID_KEY)
  if (current) return current

  const next = createDeviceId()
  localStorage.setItem(ADMIN_DEVICE_ID_KEY, next)
  return next
}

export function bootstrap(data: AdminBootstrapRequest, options: LegacyAdminKeyOptions) {
  return client.post<ApiResponse<null>>('/auth/bootstrap', data, {
    headers: legacyAdminKeyHeaders(options),
  })
}

export function resetSuperAdmin(
  data: AdminResetSuperAdminRequest,
  options: LegacyAdminKeyOptions,
) {
  return client.post<ApiResponse<null>>('/auth/reset-super-admin', data, {
    headers: legacyAdminKeyHeaders(options),
  })
}

export function login(data: AdminLoginRequest) {
  return client.post<ApiResponse<AdminLoginResponse>>('/auth/login', {
    ...data,
    deviceId: data.deviceId || getDeviceId(),
  })
}

export function getMe() {
  return client.get<ApiResponse<AdminMeResponse>>('/auth/me')
}

export function getSessions() {
  return client.get<ApiResponse<AdminSessionListItem[]>>('/auth/sessions')
}

export function deleteSession(id: string) {
  return client.delete<ApiResponse<null>>(`/auth/sessions/${id}`)
}

export function logout() {
  return client.post<ApiResponse<null>>('/auth/logout')
}
