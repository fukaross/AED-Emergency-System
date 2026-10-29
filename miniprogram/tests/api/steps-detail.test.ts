import { describe, expect, it, vi } from 'vitest'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'http://localhost:5000',
  RESTORE_AUTH_SESSION: false,
  SESSION_BACKEND_READY: false,
  USER_BACKEND_READY: false,
  USE_MOCK: true,
  CLASSROOM_BACKEND_READY: false,
}))

import { fetchQuickActionDetail } from '@/api/course'

describe('quick action detail api mock', () => {
  it('returns demo cpr steps with correct order', async () => {
    const result = await fetchQuickActionDetail('demo-quick-cpr')

    expect(result.id).toBe('demo-quick-cpr')
    expect(result.title).toBe('心肺复苏（演示卡片）')
    expect(result.steps.length).toBe(3)
    expect(result.steps[0].order).toBe(1)
    expect(result.steps[0].title).toBe('演示步骤一：内容占位')
    expect(result.steps[0].keyPoints).toContain('演示要点占位（待审核）')

    expect(result.steps[2].order).toBe(3)
    expect(result.steps[2].title).toBe('演示步骤三：内容占位')
  })

  it('returns demo airway steps', async () => {
    const result = await fetchQuickActionDetail('demo-quick-airway')

    expect(result.id).toBe('demo-quick-airway')
    expect(result.title).toBe('气道异物处理（演示卡片）')
    expect(result.steps.length).toBe(3)
    expect(result.steps[0].title).toBe('演示步骤一：内容占位')
    expect(result.steps[0].image).toBe('/static/images/course-cover-airway.png')
  })

  it('rejects unknown action id', async () => {
    await expect(fetchQuickActionDetail('unknown-action')).rejects.toThrow(
      '关键步骤不存在',
    )
  })
})
