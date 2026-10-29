import client from './client'
import type { ApiResponse } from '@/types/api'
import type {
  AdminRescuerDetail,
  AdminRescuerListResponse,
  UpdateRescuerPayload,
} from '@/types/rescuer'

export interface RescuerListParams {
  page?: number
  pageSize?: number
  campus?: string
  level?: string
  qualificationStatus?: string
  keyword?: string
}

export function getRescuers(params: RescuerListParams = {}) {
  return client.get<ApiResponse<AdminRescuerListResponse>>('/rescuers', { params })
}

export function getRescuerDetail(id: string) {
  return client.get<ApiResponse<AdminRescuerDetail>>(`/rescuers/${id}`)
}

export function updateRescuer(id: string, data: UpdateRescuerPayload) {
  return client.put<ApiResponse<AdminRescuerDetail>>(`/rescuers/${id}`, data)
}
