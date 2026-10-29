import client from './client'
import type { ApiResponse } from '@/types/api'
import type { AdminSessionListItem } from '@/types/auth'

export interface AdminAccountListItem {
  id: string
  username: string
  role: 'super_admin' | 'admin'
  status: 'active' | 'disabled'
  createdAt: string
}

export interface AdminAccountDetail extends AdminAccountListItem {
  updatedAt: string | null
  sessionsRevokedAt: string | null
  lockedUntil: string | null
  activeSessions: AdminSessionListItem[]
}

export interface AdminCreatePayload {
  username: string
  password: string
}

export interface AdminPasswordResetPayload {
  password: string
}

export interface AdminPasswordResetResponse {
  id: string
  username: string
}

export function getAdmins() {
  return client.get<ApiResponse<AdminAccountListItem[]>>('/admins')
}

export function getAdminDetail(id: string) {
  return client.get<ApiResponse<AdminAccountDetail>>(`/admins/${id}`)
}

export function createAdmin(data: AdminCreatePayload) {
  return client.post<ApiResponse<null>>('/admins', data)
}

export function resetAdminPassword(id: string, data: AdminPasswordResetPayload) {
  return client.post<ApiResponse<AdminPasswordResetResponse>>(`/admins/${id}/password-reset`, data)
}

export function deleteAdmin(id: string) {
  return client.delete<ApiResponse<null>>(`/admins/${id}`)
}
