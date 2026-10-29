import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { PublishHelpPayload, PublishHelpResponse } from '@/types/help'

const storage = new Map<string, string>()
const requestMocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}))

vi.mock('@/utils/runtime-env', () => ({
  APP_ENV: 'production',
  API_BASE_URL: 'https://api.demo.invalid',
  RESTORE_AUTH_SESSION: false,
  USE_REAL_LOCATION: false,
  USE_LOCAL_HELP_READ_MODEL: true,
  USER_BACKEND_READY: false,
  USE_MOCK: false,
  USE_REAL_WECHAT_FLOW: true,
}))

vi.mock('@/services/request', () => requestMocks)

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key)),
  setStorageSync: vi.fn((key: string, value: string) => {
    storage.set(key, value)
  }),
  removeStorageSync: vi.fn((key: string) => {
    storage.delete(key)
  }),
})

import { fetchHelpList, publishHelp } from '@/api/help'

const publishPayload: PublishHelpPayload = {
  lat: 31.2315,
  lng: 121.4785,
  address: '东区教学楼A',
  content: '需要帮助',
  type: 'general',
  requestChannel: 'user',
  privacyConsentAt: '2026-05-21T10:00:00.000Z',
}

const publishResponse: PublishHelpResponse = {
  id: 'help-real-1',
  requestNo: 'HELPREAL1',
  status: 'published',
  createdAt: '2026-05-21T10:00:01.000Z',
}

describe('help api local read model', () => {
  beforeEach(() => {
    storage.clear()
    requestMocks.get.mockReset()
    requestMocks.post.mockReset()
    requestMocks.put.mockReset()
  })

  it('keeps help list local until backend help read endpoints are ready', async () => {
    const result = await fetchHelpList(1, 20)

    expect(result.items).toEqual([])
    expect(requestMocks.get).not.toHaveBeenCalled()
  })

  it('mirrors successful real publish responses into the local help list', async () => {
    requestMocks.post.mockResolvedValueOnce(publishResponse)

    await publishHelp(publishPayload)
    const result = await fetchHelpList(1, 20)

    expect(requestMocks.post).toHaveBeenCalledWith(
      '/api/help/request',
      publishPayload,
    )
    expect(result.items).toMatchObject([
      {
        id: publishResponse.id,
        requestNo: publishResponse.requestNo,
        status: 'published',
      },
    ])
  })
})
