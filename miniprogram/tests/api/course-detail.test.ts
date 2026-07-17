import { describe, expect, it, vi } from 'vitest'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'http://localhost:5000',
  IS_PLACEHOLDER_API_BASE_URL: false,
  RESTORE_AUTH_SESSION: false,
  SESSION_BACKEND_READY: false,
  USER_BACKEND_READY: false,
  USE_MOCK: true,
  CLASSROOM_BACKEND_READY: false,
  shouldWarnPlaceholderApiBaseUrl: () => false,
}))

import { fetchCourseDetail } from '@/api/course'

describe('course detail api mock', () => {
  it('returns content and steps for article course', async () => {
    const result = await fetchCourseDetail('course-aed')

    expect(result.id).toBe('course-aed')
    expect(result.title).toBe('AED 使用教程')
    expect(result.type).toBe('article')
    expect(result.content).toBeTruthy()
    expect(result.disclaimer).toBeTruthy()
    expect(result.videos).toHaveLength(0)
    expect(result.steps.length).toBeGreaterThan(0)
    expect(result.steps[0].image).toBeTruthy()
  })

  it('returns null videoUrl for article course', async () => {
    const result = await fetchCourseDetail('course-aed')

    expect(result.videoUrl).toBeNull()
  })

  it('returns video type for wound course', async () => {
    const result = await fetchCourseDetail('course-wound')

    expect(result.type).toBe('video')
    expect(result.category).toBe('first-aid')
    expect(result.videoUrl).toBeTruthy()
    expect(result.videos).toHaveLength(1)
    expect(result.steps).toHaveLength(0)
  })

  it('rejects unknown course id', async () => {
    await expect(fetchCourseDetail('missing-course')).rejects.toThrow(
      '课程不存在',
    )
  })
})
