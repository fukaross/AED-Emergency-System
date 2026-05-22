import client from './client'
import type { ApiResponse } from '@/types/api'

export interface AdminAccountListItem {
  id: string
  username: string
  role: 'super_admin' | 'admin'
  status: 'active' | 'disabled'
  createdAt: string
}

export interface AdminCreatePayload {
  username: string
  password: string
}

export function getAdmins() {
  return client.get<ApiResponse<AdminAccountListItem[]>>('/admins')
}

export function createAdmin(data: AdminCreatePayload) {
  return client.post<ApiResponse<null>>('/admins', data)
}

export function deleteAdmin(id: string) {
  return client.delete<ApiResponse<null>>(`/admins/${id}`)
}
