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
import { COURSE_MOCK_DATA } from '@/mock/course'
import { USE_MOCK } from '@/utils/runtime-env'

const COURSE_BY_TYPE_MOCK_MAP: Readonly<Record<CourseTypeKey, CourseByTypeResponse>> = {
  aed: {
    id: 'course-aed',
    title: 'AED 使用教程',
    contentType: 'article',
    entryMode: 'detail',
    summary: 'AED 使用的核心步骤与注意事项',
  },
  cpr: {
    id: 'course-cpr',
    title: '心肺复苏教程',
    contentType: 'steps',
    entryMode: 'steps',
    summary: '心肺复苏关键步骤与节奏提示',
  },
  heimlich: {
    id: 'course-heimlich',
    title: '海姆立克急救法',
    contentType: 'steps',
    entryMode: 'steps',
    summary: '气道异物梗阻的现场急救步骤',
  },
}

function buildMockCourseList(
  filter?: CourseFilter,
): PaginatedData<CourseSummary> {
  const items = filter?.category
    ? COURSE_MOCK_DATA.filter((item) => item.category === filter.category)
    : [...COURSE_MOCK_DATA]

  return {
    items,
    total: items.length,
    page: filter?.page ?? 1,
    pageSize: filter?.pageSize ?? items.length,
    totalPages: 1,
  }
}

/** 获取课程列表 */
export function fetchCourseList(
  filter?: CourseFilter,
): Promise<PaginatedData<CourseSummary>> {
  if (USE_MOCK) {
    return Promise.resolve(buildMockCourseList(filter))
  }
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
  if (USE_MOCK) {
    return Promise.resolve(COURSE_BY_TYPE_MOCK_MAP[type])
  }
  return get<CourseByTypeResponse>('/api/courses/by-type', { type })
}

/** 获取课程详情 */
export function fetchCourseDetail(id: string): Promise<CourseDetail> {
  return get<CourseDetail>(`/api/courses/${id}`)
}
