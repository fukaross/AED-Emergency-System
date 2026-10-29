import type {
  PublishHelpPayload,
  PublishHelpResponse,
} from '@/types/help'
import { MAP_CENTER } from '@/config/site-config'
import { DEMO_REQUESTER_PHONE } from './demo-fixtures'

const MOCK_HELP_STORAGE_KEY = 'aed_mock_help_requests'
const DEFAULT_MOCK_HELP_LAT = MAP_CENTER.lat
const DEFAULT_MOCK_HELP_LNG = MAP_CENTER.lng
export const DEFAULT_MOCK_REQUESTER_PHONE = DEMO_REQUESTER_PHONE

export interface MockResponderRecord {
  readonly name: string
  readonly role: string
  readonly distance: number
  readonly estimatedArrivalTime: string
}

export interface MockTimelineRecord {
  readonly action: string
  readonly timestamp: string
  readonly note?: string
}

export interface MockHelpReportRecord {
  readonly id: string
  readonly images: readonly string[]
  readonly recordText: string
  readonly isFalseAlarm: boolean
  readonly submittedAt: string
}

export interface MockHelpRecord {
  readonly id: string
  readonly requestNo: string
  readonly content: string
  readonly type: PublishHelpPayload['type']
  readonly status: PublishHelpResponse['status']
  readonly address: string
  readonly lat: number
  readonly lng: number
  readonly requesterPhone: string
  readonly createdAt: string
  readonly images: readonly string[]
  readonly requestChannel: 'user' | 'guest'
  readonly followupCode?: string
  readonly closeToken?: string
  readonly responders: readonly MockResponderRecord[]
  readonly estimatedArrivalTime: string
  readonly timeline: readonly MockTimelineRecord[]
  readonly reports: readonly MockHelpReportRecord[]
  readonly closedAt?: string
}

const ACTIVE_MOCK_HELP_STATUSES: readonly MockHelpRecord['status'][] = [
  'published',
  'responding',
  'pending_review',
]

export function createMockHelpError(message: string): Error {
  return new Error(message)
}

export function withUpdatedHelpRecord(
  item: MockHelpRecord,
  patch: Partial<MockHelpRecord>,
): MockHelpRecord {
  return {
    ...item,
    ...patch,
  }
}

export function loadMockHelpRecords(): MockHelpRecord[] {
  try {
    const raw = uni.getStorageSync(MOCK_HELP_STORAGE_KEY) as string
    const records = raw ? (JSON.parse(raw) as MockHelpRecord[]) : []
    return normalizeMockHelpRecords(records)
  } catch {
    return []
  }
}

export function saveMockHelpRecords(records: readonly MockHelpRecord[]): void {
  try {
    uni.setStorageSync(MOCK_HELP_STORAGE_KEY, JSON.stringify(records))
  } catch {
    console.warn('[Mock Help API] 保存求助记录失败')
  }
}

export function cachePublishedHelpRecord(
  payload: PublishHelpPayload,
  result: PublishHelpResponse,
): void {
  const records = loadMockHelpRecords()
  const exists = records.some(
    (item) => item.id === result.id || item.requestNo === result.requestNo,
  )
  if (exists) return

  const record = createRecordFromPublish(payload, result)
  saveMockHelpRecords([record, ...records])
}

export function randomCode(prefix: string, length: number): string {
  const seed = Math.random().toString(36).slice(2, 2 + length).toUpperCase()
  return `${prefix}${seed}`
}

export function ensureHelpRecordById(id: string): MockHelpRecord {
  const found = loadMockHelpRecords().find(
    (item) => item.id === id || item.requestNo === id,
  )
  if (!found) {
    throw createMockHelpError('求助不存在')
  }
  return found
}

export function ensureHelpRecordByRequestNo(requestNo: string): MockHelpRecord {
  const found = loadMockHelpRecords().find((item) => item.requestNo === requestNo)
  if (!found) {
    throw createMockHelpError('求助不存在')
  }
  return found
}

export function isActiveMockHelpStatus(status: MockHelpRecord['status']): boolean {
  return ACTIVE_MOCK_HELP_STATUSES.includes(status)
}

function normalizeMockHelpRecords(
  records: readonly MockHelpRecord[],
): MockHelpRecord[] {
  let activeKept = false

  return records.map((item) => {
    const record = normalizeMockHelpRecord(item)
    if (!isActiveMockHelpStatus(record.status)) return record
    if (!activeKept) {
      activeKept = true
      return record
    }

    return closeLegacyDuplicateActiveRecord(record)
  })
}

function normalizeMockHelpRecord(record: MockHelpRecord): MockHelpRecord {
  const legacy = record as Partial<MockHelpRecord>
  return {
    ...record,
    lat: typeof legacy.lat === 'number' ? legacy.lat : DEFAULT_MOCK_HELP_LAT,
    lng: typeof legacy.lng === 'number' ? legacy.lng : DEFAULT_MOCK_HELP_LNG,
    requesterPhone: legacy.requesterPhone ?? DEFAULT_MOCK_REQUESTER_PHONE,
    reports: legacy.reports ?? [],
  }
}

function createRecordFromPublish(
  payload: PublishHelpPayload,
  result: PublishHelpResponse,
): MockHelpRecord {
  const requestChannel = payload.requestChannel === 'guest' ? 'guest' : 'user'
  return {
    id: result.id,
    requestNo: result.requestNo,
    content: payload.content,
    type: payload.type,
    status: result.status,
    address: payload.address,
    lat: payload.lat,
    lng: payload.lng,
    requesterPhone: DEFAULT_MOCK_REQUESTER_PHONE,
    createdAt: result.createdAt,
    images: payload.images ?? [],
    requestChannel,
    followupCode: result.followupCode,
    closeToken: result.closeToken,
    responders: [],
    estimatedArrivalTime: '待响应',
    timeline: [{ action: 'published', timestamp: result.createdAt }],
    reports: [],
  }
}

function closeLegacyDuplicateActiveRecord(record: MockHelpRecord): MockHelpRecord {
  const closedAt = record.closedAt ?? record.createdAt
  return withUpdatedHelpRecord(record, {
    status: 'closed',
    closedAt,
    timeline: [
      ...record.timeline,
      { action: 'closed', timestamp: closedAt, note: 'Mock 保留最近一条进行中求助' },
    ],
  })
}
