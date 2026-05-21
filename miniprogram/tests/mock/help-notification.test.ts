import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  clearMockHelpNotifications,
  dedupeMockHelpNotificationTargets,
  listMockHelpNotifications,
} from '@/mock/help-notification'
import { listMockHelpRequests, publishMockHelpRequest } from '@/mock/help'
import type { MockHelpRecord } from '@/mock/help-storage'
import type { PublishHelpPayload } from '@/types/help'

const storage = new Map<string, string>()
const MOCK_HELP_STORAGE_KEY = 'aed_mock_help_requests'

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key)),
  setStorageSync: vi.fn((key: string, value: string) => {
    storage.set(key, value)
  }),
  removeStorageSync: vi.fn((key: string) => {
    storage.delete(key)
  }),
})

function buildPayload(
  patch: Partial<PublishHelpPayload> = {},
): PublishHelpPayload {
  return {
    lat: 19.9743,
    lng: 110.5178,
    address: 'REDACTED校区教学楼A',
    content: '需要帮助',
    type: 'general',
    requestChannel: 'user',
    privacyConsentAt: '2026-05-15T10:00:00.000Z',
    ...patch,
  }
}

function buildStoredHelpRecord(
  patch: Partial<MockHelpRecord>,
): MockHelpRecord {
  const createdAt = patch.createdAt ?? '2026-05-15T10:00:00.000Z'
  return {
    id: 'help-legacy',
    requestNo: 'HELPLEGACY',
    content: '需要帮助',
    type: 'general',
    status: 'published',
    address: 'REDACTED校区教学楼A',
    lat: 19.9743,
    lng: 110.5178,
    requesterPhone: 'REDACTED',
    createdAt,
    images: [],
    requestChannel: 'user',
    responders: [],
    estimatedArrivalTime: '待响应',
    timeline: [{ action: 'published', timestamp: createdAt }],
    reports: [],
    ...patch,
  }
}

describe('mock help notifications', () => {
  beforeEach(() => {
    storage.clear()
    vi.clearAllMocks()
  })

  it('dispatches mock notifications for any published help type', () => {
    const result = publishMockHelpRequest(buildPayload({ type: 'call_school' }))
    const records = listMockHelpNotifications(result.requestNo)
    const types = new Set(records.map((item) => item.recipientType))

    expect(records.length).toBeGreaterThan(0)
    expect(types).toEqual(
      new Set(['clinic', 'counselor', 'rescuer', 'duty_teacher']),
    )
    expect(records.every((item) => item.deliveryStatus === 'mock_delivered')).toBe(
      true,
    )
  })

  it('does not include parent or guardian recipients', () => {
    const result = publishMockHelpRequest(buildPayload())
    const records = listMockHelpNotifications(result.requestNo)

    expect(records.map((item) => item.recipientType)).not.toContain('parent')
    expect(records.map((item) => item.recipientType)).not.toContain('guardian')
  })

  it('skips counselor for guest help but keeps campus and responder targets', () => {
    const result = publishMockHelpRequest(
      buildPayload({ requestChannel: 'guest' }),
    )
    const records = listMockHelpNotifications(result.requestNo)
    const types = new Set(records.map((item) => item.recipientType))

    expect(types.has('counselor')).toBe(false)
    expect(types.has('clinic')).toBe(true)
    expect(types.has('rescuer')).toBe(true)
    expect(types.has('duty_teacher')).toBe(true)
  })

  it('deduplicates recipients by user id and phone', () => {
    const deduped = dedupeMockHelpNotificationTargets([
      {
        id: 'target-1',
        type: 'rescuer',
        name: '同账号一',
        phone: 'REDACTED',
        userId: 'user-duplicate',
      },
      {
        id: 'target-2',
        type: 'duty_teacher',
        name: '同账号二',
        phone: 'REDACTED',
        userId: 'user-duplicate',
      },
      {
        id: 'target-3',
        type: 'clinic',
        name: '同电话一',
        phone: 'REDACTED',
      },
      {
        id: 'target-4',
        type: 'counselor',
        name: '同电话二',
        phone: 'REDACTED',
      },
    ])

    expect(deduped.map((item) => item.id)).toEqual(['target-1', 'target-3'])
  })

  it('clears mock notification records for local test setup', () => {
    const result = publishMockHelpRequest(buildPayload())

    expect(listMockHelpNotifications(result.requestNo).length).toBeGreaterThan(0)
    clearMockHelpNotifications()
    expect(listMockHelpNotifications(result.requestNo)).toHaveLength(0)
  })

  it('rejects a second active mock help request', () => {
    publishMockHelpRequest(buildPayload())

    expect(() => publishMockHelpRequest(buildPayload())).toThrow('已有进行中的求助')
  })

  it('normalizes legacy duplicate active records to one published item', () => {
    storage.set(
      MOCK_HELP_STORAGE_KEY,
      JSON.stringify([
        buildStoredHelpRecord({ id: 'help-new', requestNo: 'HELPNEW' }),
        buildStoredHelpRecord({ id: 'help-old', requestNo: 'HELPOLD' }),
      ]),
    )

    const result = listMockHelpRequests(1, 20)

    expect(result.items.map((item) => item.status)).toEqual(['published', 'closed'])
  })
})
