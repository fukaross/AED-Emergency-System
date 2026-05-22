import client from './client'
import type {
  AdminUserListResponse,
  AdminUserDetail,
  AdminUserUpdatePayload,
} from '@/types/user'
import type { ApiResponse } from '@/types/api'

export interface UserListParams {
  page?: number
  pageSize?: number
  keyword?: string
  accountStatus?: string
  primaryRole?: string
}

export function getUsers(params: UserListParams = {}) {
  return client.get<ApiResponse<AdminUserListResponse>>('/users', { params })
}

export function getUserDetail(openId: string) {
  return client.get<ApiResponse<AdminUserDetail>>(`/users/${openId}`)
}

export function updateUser(openId: string, data: AdminUserUpdatePayload) {
  return client.put<ApiResponse<null>>(`/users/${openId}`, data)
}
