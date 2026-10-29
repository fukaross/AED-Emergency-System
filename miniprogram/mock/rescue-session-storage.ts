import type { RescueEventAction, RescueEventType, RescueMessage } from '@/types/rescue-session'
import { MAP_CENTER } from '@/config/site-config'
import { DEMO_AVATAR_FALLBACK } from './demo-fixtures'
import { DEMO_ROLES } from './generated/demo-roles.generated'

const MOCK_RESCUE_SESSIONS_STORAGE_KEY = 'aed_mock_rescue_sessions'
const ACTIVE_RESCUER_ROLE = DEMO_ROLES.find((role) => role.role === 'active-rescuer')

export const MOCK_CURRENT_OPENID = ACTIVE_RESCUER_ROLE?.openId ?? ''
export const MOCK_CURRENT_DISPLAY_NAME = '我（急救员）'
export const ACTIVE_HELP_STATUSES = ['published', 'responding', 'pending_review'] as const

export interface MockParticipantRecord {
  openId: string
  displayName: string
  avatarUrl: string
  joinedAt: string
  lastSeenAt: string
  lastLat: number | null
  lastLng: number | null
  locationReportedAt: string | null
  lastReadSeq: number
}

export interface MockSessionRecord {
  helpId: string
  requestNo: string
  helpStatus: string
  sos: { lat: number; lng: number; address: string }
  participants: MockParticipantRecord[]
  messages: RescueMessage[]
}

interface MockSessionDatabase {
  sessions: MockSessionRecord[]
}

interface EventMessageInput {
  readonly seq: number
  readonly senderOpenId: string
  readonly senderName: string
  readonly eventType: RescueEventType
  readonly action: RescueEventAction
  readonly at: number
}

function defaultDatabase(): MockSessionDatabase {
  const now = Date.now()
  return { sessions: [defaultSession(now)] }
}

function defaultSession(now: number): MockSessionRecord {
  return {
    helpId: 'mock-help-1',
    requestNo: 'HELP-MOCK-1',
    helpStatus: 'published',
    sos: { lat: MAP_CENTER.lat, lng: MAP_CENTER.lng, address: '演示求助点（合成位置）' },
    participants: [defaultParticipant(now)],
    messages: defaultMessages(now),
  }
}

function defaultParticipant(now: number): MockParticipantRecord {
  return {
    openId: 'demo-openid-rescuer-west',
    displayName: '演示急救员（西区）',
    avatarUrl: DEMO_AVATAR_FALLBACK,
    joinedAt: new Date(now - 120_000).toISOString(),
    lastSeenAt: new Date(now - 10_000).toISOString(),
    lastLat: MAP_CENTER.lat + 0.0003,
    lastLng: MAP_CENTER.lng - 0.0006,
    locationReportedAt: new Date(now - 10_000).toISOString(),
    lastReadSeq: 2,
  }
}

function defaultMessages(now: number): RescueMessage[] {
  return [
    createEventMessage({
      seq: 1,
      senderOpenId: 'demo-openid-rescuer-west',
      senderName: '演示急救员（西区）',
      eventType: 'joined',
      action: 'mark',
      at: now - 120_000,
    }),
    createEventMessage({
      seq: 2,
      senderOpenId: 'demo-openid-rescuer-west',
      senderName: '演示急救员（西区）',
      eventType: 'taken_aed',
      action: 'mark',
      at: now - 60_000,
    }),
  ]
}

export function createEventMessage(input: EventMessageInput): RescueMessage {
  return {
    id: `mock-msg-${input.seq}-${input.at}`,
    seq: input.seq,
    kind: 'status_event',
    senderOpenId: input.senderOpenId,
    senderName: input.senderName,
    text: null,
    imageUrl: null,
    eventType: input.eventType,
    action: input.action,
    createdAt: new Date(input.at).toISOString(),
  }
}

export function loadDatabase(): MockSessionDatabase {
  try {
    const raw = uni.getStorageSync(MOCK_RESCUE_SESSIONS_STORAGE_KEY) as string
    return raw ? (JSON.parse(raw) as MockSessionDatabase) : defaultDatabase()
  } catch {
    return defaultDatabase()
  }
}

export function saveDatabase(database: MockSessionDatabase): void {
  try {
    uni.setStorageSync(MOCK_RESCUE_SESSIONS_STORAGE_KEY, JSON.stringify(database))
  } catch {
    console.warn('[Mock Rescue API] 保存会话数据失败')
  }
}

export function ensureSession(
  database: MockSessionDatabase,
  helpId: string,
): MockSessionRecord {
  let session = database.sessions.find((item) => item.helpId === helpId)
  if (session) return session

  session = {
    helpId,
    requestNo: `HELP-${helpId}`,
    helpStatus: 'published',
    sos: { lat: MAP_CENTER.lat, lng: MAP_CENTER.lng, address: '演示求助点（合成位置）' },
    participants: [],
    messages: [],
  }
  database.sessions.push(session)
  return session
}

export function ensureParticipant(session: MockSessionRecord): MockParticipantRecord {
  const participant = session.participants.find((item) => item.openId === MOCK_CURRENT_OPENID)
  if (!participant) throw new Error('仅会话参与者可执行该操作')
  return participant
}
