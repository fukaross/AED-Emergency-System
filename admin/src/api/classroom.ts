import client from './client'
import type { ApiResponse } from '@/types/api'
import type {
  ClassroomSummary,
  AdminCourseListResponse,
  AdminCourseItem,
  AdminCourseDetail,
  CreateCoursePayload,
  UpdateCoursePayload,
  AdminQuickActionItem,
  AdminQuickActionDetail,
  CreateQuickActionPayload,
  UpdateQuickActionPayload,
} from '@/types/classroom'

export function getKnowledgeSummary() {
  return client.get<ApiResponse<ClassroomSummary>>('/admin/knowledge-summary')
}

export function updateKnowledgeSummary(data: { items: { key: string; title: string; value: number; sortOrder: number }[] }) {
  return client.put<ApiResponse<null>>('/admin/knowledge-summary', data)
}

export interface CourseListParams {
  page?: number
  pageSize?: number
  category?: string
  keyword?: string
}

export function getCourses(params: CourseListParams = {}) {
  return client.get<ApiResponse<AdminCourseListResponse>>('/admin/courses', { params })
}

export function getCourseDetail(id: string) {
  return client.get<ApiResponse<AdminCourseDetail>>(`/admin/courses/${id}`)
}

export function createCourse(data: CreateCoursePayload) {
  return client.post<ApiResponse<AdminCourseItem>>('/admin/courses', data)
}

export function updateCourse(id: string, data: UpdateCoursePayload) {
  return client.put<ApiResponse<AdminCourseItem>>(`/admin/courses/${id}`, data)
}

export function deleteCourse(id: string) {
  return client.delete<ApiResponse<null>>(`/admin/courses/${id}`)
}

export function getQuickActions() {
  return client.get<ApiResponse<AdminQuickActionItem[]>>('/admin/quick-actions')
}

export function getQuickActionDetail(id: string) {
  return client.get<ApiResponse<AdminQuickActionDetail>>(`/admin/quick-actions/${id}`)
}

export function createQuickAction(data: CreateQuickActionPayload) {
  return client.post<ApiResponse<AdminQuickActionDetail>>('/admin/quick-actions', data)
}

export function updateQuickAction(id: string, data: UpdateQuickActionPayload) {
  return client.put<ApiResponse<AdminQuickActionDetail>>(`/admin/quick-actions/${id}`, data)
}

export function deleteQuickAction(id: string) {
  return client.delete<ApiResponse<null>>(`/admin/quick-actions/${id}`)
}
