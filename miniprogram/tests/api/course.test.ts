import { describe, expect, it, vi } from 'vitest'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'http://localhost:5000',
  IS_PLACEHOLDER_API_BASE_URL: false,
  RESTORE_AUTH_SESSION: false,
  USE_MOCK: true,
  CLASSROOM_BACKEND_READY: false,
  shouldWarnPlaceholderApiBaseUrl: () => false,
}))

import {
  fetchCourseDetail,
  fetchCourseList,
  fetchQuickActionDetail,
  fetchQuickActions,
} from '@/api/course'

describe('course api mock', () => {
  it('paginates course list mock data like the backend contract', async () => {
    const result = await fetchCourseList({ page: 2, pageSize: 2 })

    expect(result.total).toBe(4)
    expect(result.page).toBe(2)
    expect(result.pageSize).toBe(2)
    expect(result.totalPages).toBe(2)
    expect(result.items.map((item) => item.id)).toEqual([
      'course-heimlich',
      'course-wound',
    ])
  })

  it('returns video list for video course mock data', async () => {
    const result = await fetchCourseDetail('course-wound')

    expect(result.videoUrl).toBeTruthy()
    expect(result.videos).toHaveLength(1)
  })

  it('rejects unknown course ids in mock mode', async () => {
    await expect(fetchCourseDetail('missing-course')).rejects.toThrow('课程不存在')
  })

  it('returns quick action mock data for the classroom home page', async () => {
    const result = await fetchQuickActions()

    expect(result.map((item) => item.id)).toEqual(['qa-01', 'qa-02'])
    expect(result[0].steps.map((step) => step.title)).toContain('判断意识')
  })

  it('rejects unknown quick action ids in mock mode', async () => {
    await expect(fetchQuickActionDetail('missing-action')).rejects.toThrow(
      '关键步骤不存在',
    )
  })
})
