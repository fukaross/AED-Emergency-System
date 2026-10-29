// 课程 Mock 数据 —— 来自 demo/data/courses.json 生成产物（演示占位内容，无真实教材）。
import type { CourseDetail, CourseSummary } from '@/types/user'
import { DEMO_COURSES } from './generated/demo-classroom.generated'

const enabledCourses = DEMO_COURSES.filter((course) => course.isEnabled)

/** 与后端一致的排序：pinned 优先 → sortOrder 升序 → createdAt 降序。 */
function sortCourses<T extends { pinned: boolean; sortOrder: number; createdAt: string }>(
  left: T,
  right: T,
): number {
  if (left.pinned !== right.pinned) return left.pinned ? -1 : 1
  if (left.sortOrder !== right.sortOrder) return left.sortOrder - right.sortOrder
  return right.createdAt.localeCompare(left.createdAt)
}

const mockCourseViewCounts = new Map<string, number>(
  enabledCourses.map((course) => [course.id, course.viewCount]),
)

/** 读取 Mock 课程当前阅读量 */
export function getMockCourseViewCount(id: string): number {
  return mockCourseViewCounts.get(id) ?? 0
}

/** Mock 课程阅读量 +1 并返回新值；课程不存在时返回 0 */
export function incrementMockCourseView(id: string): number {
  if (!mockCourseViewCounts.has(id)) return 0
  const next = getMockCourseViewCount(id) + 1
  mockCourseViewCounts.set(id, next)
  return next
}

export function resetMockCourseViews(): void {
  for (const course of enabledCourses) {
    mockCourseViewCounts.set(course.id, course.viewCount)
  }
}

export const COURSE_MOCK_DATA: readonly CourseSummary[] = [...enabledCourses]
  .sort(sortCourses)
  .map((course) => ({
    id: course.id,
    title: course.title,
    thumbnail: course.thumbnail,
    type: course.type,
    category: course.category,
    createdAt: course.createdAt,
    pinned: course.pinned,
    sortOrder: course.sortOrder,
    cacheVersion: course.cacheVersion,
    estimatedTime: course.estimatedTime,
  }))

export const COURSE_DETAIL_MOCK_DATA: readonly CourseDetail[] = enabledCourses.map(
  (course) => ({
    id: course.id,
    title: course.title,
    thumbnail: course.thumbnail,
    type: course.type,
    category: course.category,
    createdAt: course.createdAt,
    pinned: course.pinned,
    sortOrder: course.sortOrder,
    cacheVersion: course.cacheVersion,
    estimatedTime: course.estimatedTime,
    content: course.content,
    videoUrl: course.videoUrl,
    videos: [...course.videos],
    disclaimer: course.disclaimer,
    steps: course.steps.map((step) => ({ ...step, keyPoints: [...step.keyPoints] })),
    viewCount: getMockCourseViewCount(course.id),
  }),
)
