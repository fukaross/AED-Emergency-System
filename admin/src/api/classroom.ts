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
  return client.get<ApiResponse<ClassroomSummary>>('/knowledge-summary')
}

export function updateKnowledgeSummary(data: { items: { key: string; title: string; value: number; sortOrder: number }[] }) {
  return client.put<ApiResponse<null>>('/knowledge-summary', data)
}

export interface CourseListParams {
  page?: number
  pageSize?: number
  category?: string
  keyword?: string
}

export function getCourses(params: CourseListParams = {}) {
  return client.get<ApiResponse<AdminCourseListResponse>>('/courses', { params })
}

export function getCourseDetail(id: string) {
  return client.get<ApiResponse<AdminCourseDetail>>(`/courses/${id}`)
}

export function createCourse(data: CreateCoursePayload) {
  return client.post<ApiResponse<AdminCourseItem>>('/courses', data)
}

export function updateCourse(id: string, data: UpdateCoursePayload) {
  return client.put<ApiResponse<AdminCourseItem>>(`/courses/${id}`, data)
}

export function deleteCourse(id: string) {
  return client.delete<ApiResponse<null>>(`/courses/${id}`)
}

export function getQuickActions() {
  return client.get<ApiResponse<AdminQuickActionItem[]>>('/quick-actions')
}

export function getQuickActionDetail(id: string) {
  return client.get<ApiResponse<AdminQuickActionDetail>>(`/quick-actions/${id}`)
}

export function createQuickAction(data: CreateQuickActionPayload) {
  return client.post<ApiResponse<AdminQuickActionDetail>>('/quick-actions', data)
}

export function updateQuickAction(id: string, data: UpdateQuickActionPayload) {
  return client.put<ApiResponse<AdminQuickActionDetail>>(`/quick-actions/${id}`, data)
}

export function deleteQuickAction(id: string) {
  return client.delete<ApiResponse<null>>(`/quick-actions/${id}`)
}
