import { describe, expect, it, vi } from 'vitest'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'http://localhost:5000',
  IS_PLACEHOLDER_API_BASE_URL: false,
  RESTORE_AUTH_SESSION: false,
  USER_BACKEND_READY: false,
  USE_MOCK: true,
  CLASSROOM_BACKEND_READY: false,
  shouldWarnPlaceholderApiBaseUrl: () => false,
}))

import { fetchKnowledgeSummary } from '@/api/knowledge'

describe('knowledge api mock', () => {
  it('returns the classroom summary stats in display order', async () => {
    const result = await fetchKnowledgeSummary()

    expect(result.items.map((item) => item.key)).toEqual([
      'aed_count',
      'trained_people',
      'certified_people',
      'coverage_rate',
    ])
    expect(result.items.map((item) => item.value)).toEqual([
      24,
      6392,
      2592,
      52.6,
    ])
    expect(result.items.map((item) => item.title)).toEqual([
      'AED数量',
      '培训人数',
      '取证人数',
      '覆盖率(%)',
    ])
  })
})
