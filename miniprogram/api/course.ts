// 课程相关的接口 —— 列表、详情、按类型获取预置课程
import { get } from '@/services/request'
import type {
  CourseSummary,
  CourseDetail,
  CourseFilter,
  CourseTypeKey,
  CourseByTypeResponse,
} from '@/types/user'
import type { PaginatedData } from '@/types/common'
import { COURSE_DETAIL_MOCK_DATA, COURSE_MOCK_DATA } from '@/mock/course'
import { USE_MOCK } from '@/utils/runtime-env'

const DEFAULT_COURSE_PAGE = 1
const MIN_COURSE_PAGE_SIZE = 1

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
  const allItems = filter?.category
    ? COURSE_MOCK_DATA.filter((item) => item.category === filter.category)
    : [...COURSE_MOCK_DATA]
  const page = Math.max(DEFAULT_COURSE_PAGE, filter?.page ?? DEFAULT_COURSE_PAGE)
  const pageSize = Math.max(
    MIN_COURSE_PAGE_SIZE,
    filter?.pageSize ?? allItems.length,
  )
  const start = (page - 1) * pageSize

  return {
    items: allItems.slice(start, start + pageSize),
    total: allItems.length,
    page,
    pageSize,
    totalPages: Math.max(1, Math.ceil(allItems.length / pageSize)),
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
 * 按课程类型获取预置课程（类型→课程的映射由后端决定，前端别自己硬编码）
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
  if (USE_MOCK) {
    const found = COURSE_DETAIL_MOCK_DATA.find((item) => item.id === id)
    if (!found) {
      return Promise.reject(new Error('课程不存在'))
    }
    return Promise.resolve(found)
  }

  return get<CourseDetail>(`/api/courses/${id}`)
}
