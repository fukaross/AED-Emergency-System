import client from './client'
import type { AdminHelpListResponse, AdminHelpDetailResponse } from '@/types/help'
import type { ApiResponse } from '@/types/api'

export interface HelpListParams {
  page?: number
  pageSize?: number
  status?: string
  keyword?: string
}

export function getHelpRequests(params: HelpListParams = {}) {
  return client.get<ApiResponse<AdminHelpListResponse>>('/help-requests', { params })
}

export function getHelpRequestDetail(id: string) {
  return client.get<ApiResponse<AdminHelpDetailResponse>>(`/help-requests/${id}`)
}

export interface CloseHelpPayload {
  status: 'closed' | 'false_alarm'
  note?: string
}

export function closeHelpRequest(id: string, data: CloseHelpPayload) {
  return client.put<ApiResponse<null>>(`/help-requests/${id}/close`, data)
}
