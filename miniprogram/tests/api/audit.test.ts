import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@/services/request', () => ({
  post: vi.fn(() => Promise.resolve()),
}))

vi.mock('@/services/auth-service', () => ({
  getToken: vi.fn(() => ''),
}))

vi.mock('@/utils/runtime-env', () => ({
  USE_MOCK: false,
}))

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => {
    if (key === 'aed_current_user_id') {
      return 'user-001'
    }

    return ''
  }),
  getSystemInfoSync: vi.fn(() => ({
    model: 'iPhone15,2',
  })),
})

const getAccountInfoSync = vi.fn(() => ({
  miniProgram: {
    version: '0.1.0',
  },
}))

;(uni as typeof uni & { getAccountInfoSync?: typeof getAccountInfoSync }).getAccountInfoSync =
  getAccountInfoSync

import { submitAuditBatch } from '@/api/audit'
import { post } from '@/services/request'

describe('audit api', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('adds required contract fields before submitting audit events', async () => {
    await submitAuditBatch([
      {
        eventType: 'rescuer',
        eventName: 'rescuer.view_contact',
        targetId: 'rescuer-001',
      },
    ])

    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith(
      '/api/audit/batch',
      {
        events: [
          expect.objectContaining({
            eventType: 'rescuer',
            eventName: 'rescuer.view_contact',
            targetId: 'rescuer-001',
            userId: 'user-001',
            timestamp: expect.any(String),
            clientInfo: {
              platform: 'mp-weixin',
              version: '0.1.0',
              deviceModel: 'iPhone15,2',
            },
          }),
        ],
      },
      { skipErrorHandler: true },
    )
  })
})
