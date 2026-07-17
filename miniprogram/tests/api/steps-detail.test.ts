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

import { fetchQuickActionDetail } from '@/api/course'

describe('quick action detail api mock', () => {
  it('returns cpr steps with correct order', async () => {
    const result = await fetchQuickActionDetail('qa-01')

    expect(result.id).toBe('qa-01')
    expect(result.title).toBe('心肺复苏')
    expect(result.steps.length).toBe(3)
    expect(result.steps[0].order).toBe(1)
    expect(result.steps[0].title).toBe('判断意识')
    expect(result.steps[0].keyPoints).toContain('轻拍重唤')

    expect(result.steps[2].order).toBe(3)
    expect(result.steps[2].title).toBe('胸外按压')
  })

  it('returns heimlich steps', async () => {
    const result = await fetchQuickActionDetail('qa-02')

    expect(result.id).toBe('qa-02')
    expect(result.title).toBe('海姆立克急救法')
    expect(result.steps.length).toBe(3)
    expect(result.steps[0].title).toBe('确认梗阻')
    expect(result.steps[0].keyPoints).toContain('患者双手掐喉是典型信号')
  })

  it('rejects unknown action id', async () => {
    await expect(fetchQuickActionDetail('unknown-action')).rejects.toThrow(
      '关键步骤不存在',
    )
  })
})
