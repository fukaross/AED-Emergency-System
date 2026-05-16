import { describe, expect, it, vi } from 'vitest'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'http://localhost:5000',
  IS_PLACEHOLDER_API_BASE_URL: false,
  USE_MOCK: true,
  shouldWarnPlaceholderApiBaseUrl: () => false,
}))

import { fetchCourseDetail, fetchCourseList } from '@/api/course'

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

  it('does not point course detail mock data at missing video assets', async () => {
    const result = await fetchCourseDetail('course-wound')

    expect(result.videoUrl).toBeNull()
  })

  it('rejects unknown course ids in mock mode', async () => {
    await expect(fetchCourseDetail('missing-course')).rejects.toThrow('课程不存在')
  })
})
