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

function legacyAdminKeyHeaders(options: LegacyAdminKeyOptions) {
  return { 'X-Aed-Admin-Key': options.adminKey }
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
  return client.post<ApiResponse<AdminLoginResponse>>('/auth/login', data)
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
