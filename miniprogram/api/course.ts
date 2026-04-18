/**
 * 课程相关 API
 *
 * 接口来源：CLAUDE.md §8.1 + BackEnd.md §4.4
 *
 * GET /api/courses          — 课程列表 [公开]
 * GET /api/courses/by-type  — 按类型获取预置课程 [公开]
 * GET /api/courses/:id      — 课程详情 [公开]
 */
import { get } from '@/services/request'
import type {
  CourseSummary,
  CourseDetail,
  CourseFilter,
  CourseTypeKey,
  CourseByTypeResponse,
} from '@/types/user'
import type { PaginatedData } from '@/types/common'

/** 获取课程列表 */
export function fetchCourseList(
  filter?: CourseFilter,
): Promise<PaginatedData<CourseSummary>> {
  return get<PaginatedData<CourseSummary>>(
    '/api/courses',
    filter as unknown as Record<string, unknown>,
  )
}

/**
 * 按课程类型获取预置课程
 * 前端禁止维护 type → courseId 硬编码映射
 */
export function fetchCourseByType(
  type: CourseTypeKey,
): Promise<CourseByTypeResponse> {
  return get<CourseByTypeResponse>('/api/courses/by-type', { type })
}

/** 获取课程详情 */
export function fetchCourseDetail(id: string): Promise<CourseDetail> {
  return get<CourseDetail>(`/api/courses/${id}`)
}
