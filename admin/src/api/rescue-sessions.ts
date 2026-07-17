import client from './client'
import type { ApiResponse } from '@/types/api'
import type {
  AdminRescueSessionDetail,
  AdminRescueSessionListResponse,
} from '@/types/rescue-session'

export interface RescueSessionListParams {
  /** active（进行中，默认） / history（已关闭存档） */
  status?: 'active' | 'history'
  page?: number
  pageSize?: number
}

export function getRescueSessions(params: RescueSessionListParams = {}) {
  return client.get<ApiResponse<AdminRescueSessionListResponse>>('/rescue-sessions', { params })
}

export function getRescueSessionDetail(helpId: string, signal?: AbortSignal) {
  return client.get<ApiResponse<AdminRescueSessionDetail>>(`/rescue-sessions/${helpId}`, { signal })
}
