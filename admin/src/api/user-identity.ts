import client from './client'
import type { ApiResponse } from '@/types/api'
import type {
  AdminResponderClaimDetail,
  AdminResponderClaimListResponse,
  AdminResponderClaimReviewResponse,
  AdminSchoolBindingDetail,
  AdminSchoolBindingListResponse,
  AdminSchoolBindingReviewResponse,
  AdminSchoolProfileDetail,
  AdminSchoolProfileListResponse,
  ApproveResponderClaimPayload,
  ApproveSchoolBindingPayload,
  IdentityApplicationStatus,
  RejectIdentityPayload,
  SchoolPersonType,
  SchoolProfilePayload,
} from '@/types/user-identity'

export interface SchoolProfileListParams {
  page?: number
  pageSize?: number
  personType?: SchoolPersonType | 'all'
  keyword?: string
}

export interface IdentityApplicationListParams {
  page?: number
  pageSize?: number
  status?: IdentityApplicationStatus | 'all'
  keyword?: string
}

export function getSchoolProfiles(params: SchoolProfileListParams = {}) {
  return client.get<ApiResponse<AdminSchoolProfileListResponse>>('/school-profiles', { params })
}

export function getSchoolProfileDetail(id: string) {
  return client.get<ApiResponse<AdminSchoolProfileDetail>>(`/school-profiles/${id}`)
}

export function createSchoolProfile(data: SchoolProfilePayload) {
  return client.post<ApiResponse<AdminSchoolProfileDetail>>('/school-profiles', data)
}

export function updateSchoolProfile(id: string, data: SchoolProfilePayload) {
  return client.put<ApiResponse<AdminSchoolProfileDetail>>(`/school-profiles/${id}`, data)
}

export function deleteSchoolProfile(id: string) {
  return client.delete<ApiResponse<null>>(`/school-profiles/${id}`)
}

export function getSchoolBindings(params: IdentityApplicationListParams = {}) {
  return client.get<ApiResponse<AdminSchoolBindingListResponse>>('/school-bindings', { params })
}

export function getSchoolBindingDetail(id: string) {
  return client.get<ApiResponse<AdminSchoolBindingDetail>>(`/school-bindings/${id}`)
}

export function approveSchoolBinding(id: string, data: ApproveSchoolBindingPayload) {
  return client.put<ApiResponse<AdminSchoolBindingReviewResponse>>(
    `/school-bindings/${id}/approve`,
    data,
  )
}

export function rejectSchoolBinding(id: string, data: RejectIdentityPayload) {
  return client.put<ApiResponse<AdminSchoolBindingReviewResponse>>(
    `/school-bindings/${id}/reject`,
    data,
  )
}

export function deleteSchoolBinding(id: string) {
  return client.delete<ApiResponse<null>>(`/school-bindings/${id}`)
}

export function getResponderClaims(params: IdentityApplicationListParams = {}) {
  return client.get<ApiResponse<AdminResponderClaimListResponse>>('/responder-claims', { params })
}

export function getResponderClaimDetail(id: string) {
  return client.get<ApiResponse<AdminResponderClaimDetail>>(`/responder-claims/${id}`)
}

export function approveResponderClaim(id: string, data: ApproveResponderClaimPayload) {
  return client.put<ApiResponse<AdminResponderClaimReviewResponse>>(
    `/responder-claims/${id}/approve`,
    data,
  )
}

export function rejectResponderClaim(id: string, data: RejectIdentityPayload) {
  return client.put<ApiResponse<AdminResponderClaimReviewResponse>>(
    `/responder-claims/${id}/reject`,
    data,
  )
}
