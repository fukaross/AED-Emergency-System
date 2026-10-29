import { describe, expect, it, vi } from 'vitest'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'http://localhost:5000',
  RESTORE_AUTH_SESSION: false,
  SESSION_BACKEND_READY: false,
  USER_BACKEND_READY: false,
  USE_MOCK: true,
  CLASSROOM_BACKEND_READY: false,
}))

import { fetchCourseDetail } from '@/api/course'

describe('course detail api mock', () => {
  it('returns content and steps for demo article course', async () => {
    const result = await fetchCourseDetail('demo-course-aed')

    expect(result.id).toBe('demo-course-aed')
    expect(result.title).toBe('AED 使用演示课程')
    expect(result.type).toBe('article')
    expect(result.content).toContain('演示占位内容')
    expect(result.disclaimer).toContain('演示内容')
    expect(result.videos).toHaveLength(0)
    expect(result.steps.length).toBeGreaterThan(0)
    expect(result.steps[0].image).toBeTruthy()
  })

  it('returns null videoUrl for article course', async () => {
    const result = await fetchCourseDetail('demo-course-aed')

    expect(result.videoUrl).toBeNull()
  })

  it('keeps the first-aid category filterable and article-only', async () => {
    const result = await fetchCourseDetail('demo-course-wound')

    expect(result.type).toBe('article')
    expect(result.category).toBe('first-aid')
    expect(result.videoUrl).toBeNull()
    expect(result.videos).toHaveLength(0)
    expect(result.steps).toHaveLength(1)
  })

  it('rejects unknown course id', async () => {
    await expect(fetchCourseDetail('missing-course')).rejects.toThrow(
      '课程不存在',
    )
  })
})
