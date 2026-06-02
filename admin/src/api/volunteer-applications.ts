import client from './client'
import type { ApiResponse } from '@/types/api'
import type {
  AdminVolunteerApplicationDetail,
  AdminVolunteerApplicationListResponse,
  AdminVolunteerApplicationReviewResponse,
  ApproveVolunteerApplicationPayload,
  RejectVolunteerApplicationPayload,
  VolunteerApplicationStatus,
} from '@/types/volunteer-application'

export interface VolunteerApplicationListParams {
  page?: number
  pageSize?: number
  status?: VolunteerApplicationStatus | 'all'
  keyword?: string
}

export function getVolunteerApplications(params: VolunteerApplicationListParams = {}) {
  return client.get<ApiResponse<AdminVolunteerApplicationListResponse>>(
    '/volunteer-applications',
    { params },
  )
}

export function getVolunteerApplicationDetail(id: string) {
  return client.get<ApiResponse<AdminVolunteerApplicationDetail>>(
    `/volunteer-applications/${id}`,
  )
}

export function approveVolunteerApplication(
  id: string,
  data: ApproveVolunteerApplicationPayload,
) {
  return client.put<ApiResponse<AdminVolunteerApplicationReviewResponse>>(
    `/volunteer-applications/${id}/approve`,
    data,
  )
}

export function rejectVolunteerApplication(
  id: string,
  data: RejectVolunteerApplicationPayload,
) {
  return client.put<ApiResponse<AdminVolunteerApplicationReviewResponse>>(
    `/volunteer-applications/${id}/reject`,
    data,
  )
}
