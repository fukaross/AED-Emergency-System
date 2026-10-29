import { describe, expect, it, vi } from 'vitest'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'http://localhost:5000',
  RESTORE_AUTH_SESSION: false,
  SESSION_BACKEND_READY: false,
  USER_BACKEND_READY: false,
  USE_MOCK: true,
  CLASSROOM_BACKEND_READY: false,
}))

import {
  fetchCourseDetail,
  fetchCourseList,
  fetchQuickActionDetail,
  fetchQuickActions,
  incrementCourseView,
} from '@/api/course'

describe('course api mock', () => {
  it('paginates demo course list with pinned-first ordering', async () => {
    const result = await fetchCourseList({ page: 2, pageSize: 2 })

    expect(result.total).toBe(4)
    expect(result.page).toBe(2)
    expect(result.pageSize).toBe(2)
    expect(result.totalPages).toBe(2)
    expect(result.items.map((item) => item.id)).toEqual([
      'demo-course-airway',
      'demo-course-wound',
    ])
  })

  it('keeps every demo course article-only (no unlicensed video data)', async () => {
    const result = await fetchCourseDetail('demo-course-wound')
    const all = await fetchCourseList()

    expect(result.type).toBe('article')
    expect(result.videoUrl).toBeNull()
    expect(result.videos).toHaveLength(0)
    expect(all.items.every((item) => item.type === 'article')).toBe(true)
    expect(result.steps.length).toBeGreaterThan(0)
  })

  it('rejects unknown course ids in mock mode', async () => {
    await expect(fetchCourseDetail('missing-course')).rejects.toThrow('课程不存在')
  })

  it('returns demo quick action cards for the classroom home page', async () => {
    const result = await fetchQuickActions()

    expect(result.map((item) => item.id)).toEqual([
      'demo-quick-cpr',
      'demo-quick-airway',
    ])
    expect(result[0].steps.map((step) => step.title)).toContain(
      '演示步骤一：内容占位',
    )
  })

  it('rejects unknown quick action ids in mock mode', async () => {
    await expect(fetchQuickActionDetail('missing-action')).rejects.toThrow(
      '关键步骤不存在',
    )
  })

  it('counts each mock course view repeatedly and reflects it in detail', async () => {
    const first = await incrementCourseView('demo-course-airway')
    const second = await incrementCourseView('demo-course-airway')

    expect(second.viewCount).toBe(first.viewCount + 1)

    const detail = await fetchCourseDetail('demo-course-airway')
    expect(detail.viewCount).toBe(second.viewCount)
  })

  it('returns zero view count for unknown mock course', async () => {
    const result = await incrementCourseView('missing-course')

    expect(result.viewCount).toBe(0)
  })
})
