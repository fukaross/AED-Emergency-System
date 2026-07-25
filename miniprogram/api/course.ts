// 课程相关的接口 —— 列表、详情、关键步骤
import { get, post } from '@/services/request'
import type {
  CourseSummary,
  CourseDetail,
  CourseFilter,
  CourseViewResult,
} from '@/types/user'
import type { PaginatedData } from '@/types/common'
import type { QuickAction } from '@/types/knowledge'
import {
  COURSE_DETAIL_MOCK_DATA,
  COURSE_MOCK_DATA,
  getMockCourseViewCount,
  incrementMockCourseView,
} from '@/mock/course'
import { QUICK_ACTION_MOCK_DATA } from '@/mock/knowledge'
import { USE_MOCK, CLASSROOM_BACKEND_READY } from '@/utils/runtime-env'

const DEFAULT_COURSE_PAGE = 1
const MIN_COURSE_PAGE_SIZE = 1

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
  if (!CLASSROOM_BACKEND_READY && USE_MOCK) {
    return Promise.resolve(buildMockCourseList(filter))
  }
  return get<PaginatedData<CourseSummary>>(
    '/api/courses',
    filter as unknown as Record<string, unknown>,
  )
}

/** 获取课程详情 */
export function fetchCourseDetail(id: string): Promise<CourseDetail> {
  if (!CLASSROOM_BACKEND_READY && USE_MOCK) {
    const found = COURSE_DETAIL_MOCK_DATA.find((item) => item.id === id)
    if (!found) {
      return Promise.reject(new Error('课程不存在'))
    }
    return Promise.resolve({ ...found, viewCount: getMockCourseViewCount(id) })
  }

  return get<CourseDetail>(`/api/courses/${id}`)
}

/** 课程阅读量 +1（公开接口，游客与重复进入均计数），返回最新阅读量 */
export function incrementCourseView(id: string): Promise<CourseViewResult> {
  if (!CLASSROOM_BACKEND_READY && USE_MOCK) {
    return Promise.resolve({ viewCount: incrementMockCourseView(id) })
  }

  return post<CourseViewResult>(
    `/api/courses/${id}/views`,
    undefined,
    { skipErrorHandler: true, skipAuthRefresh: true },
  )
}

/** 获取关键步骤快速操作卡片列表 */
export function fetchQuickActions(): Promise<QuickAction[]> {
  if (!CLASSROOM_BACKEND_READY && USE_MOCK) {
    return Promise.resolve([...QUICK_ACTION_MOCK_DATA])
  }
  return get<QuickAction[]>('/api/quick-actions')
}

/** 获取关键步骤详情 */
export function fetchQuickActionDetail(id: string): Promise<QuickAction> {
  if (!CLASSROOM_BACKEND_READY && USE_MOCK) {
    const found = QUICK_ACTION_MOCK_DATA.find((item) => item.id === id)
    if (!found) {
      return Promise.reject(new Error('关键步骤不存在'))
    }
    return Promise.resolve(found)
  }
  return get<QuickAction>(`/api/quick-actions/${id}`)
}
