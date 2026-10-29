// 急救会话链路的 Mock：HTTP API 行为与 BackEnd.md §4.5A 保持一致。
import type {
  RescueEventAction,
  RescueMessage,
  RescueMineItem,
  RescueMutableEventType,
  RescueSessionSnapshot,
  RescueStatusEventResult,
  RescueView,
} from '@/types/rescue-session'
import type { GeoPoint } from '@/types/common'
import { MESSAGE_TEXT_MAX_LENGTH } from '@/utils/rescue-session-constants'
import {
  isMarked,
  nextSeq,
  toMineItem,
  toRescueView,
  toSnapshot,
} from '@/mock/rescue-session-projection'
import {
  ACTIVE_HELP_STATUSES,
  createEventMessage,
  ensureParticipant,
  ensureSession,
  loadDatabase,
  MOCK_CURRENT_DISPLAY_NAME,
  MOCK_CURRENT_OPENID,
  saveDatabase,
} from '@/mock/rescue-session-storage'

export function joinMockRescueSession(helpId: string): RescueSessionSnapshot {
  const database = loadDatabase()
  const session = ensureSession(database, helpId)
  const now = new Date().toISOString()
  let current = session.participants.find((item) => item.openId === MOCK_CURRENT_OPENID)

  if (current) {
    current.lastSeenAt = now
  } else {
    current = {
      openId: MOCK_CURRENT_OPENID,
      displayName: MOCK_CURRENT_DISPLAY_NAME,
      avatarUrl: '',
      joinedAt: now,
      lastSeenAt: now,
      lastLat: null,
      lastLng: null,
      locationReportedAt: null,
      lastReadSeq: 0,
    }
    session.participants.push(current)
    session.messages.push(
      createEventMessage({
        seq: nextSeq(session),
        senderOpenId: MOCK_CURRENT_OPENID,
        senderName: MOCK_CURRENT_DISPLAY_NAME,
        eventType: 'joined',
        action: 'mark',
        at: Date.now(),
      }),
    )
    if (session.helpStatus === 'published') session.helpStatus = 'responding'
  }

  saveDatabase(database)
  return toSnapshot(session, current)
}

export function fetchMockRescueSession(helpId: string, afterSeq?: number): RescueSessionSnapshot {
  const database = loadDatabase()
  const session = ensureSession(database, helpId)
  const current = ensureParticipant(session)
  const snapshot = toSnapshot(session, current, afterSeq)
  current.lastReadSeq = Math.max(current.lastReadSeq, snapshot.latestSeq)
  saveDatabase(database)
  return snapshot
}

export function postMockRescueMessage(helpId: string, text: string): RescueMessage {
  const trimmed = text.trim()
  if (!trimmed || trimmed.length > MESSAGE_TEXT_MAX_LENGTH) {
    throw new Error('text 需为 1~500 字符')
  }

  const database = loadDatabase()
  const session = ensureSession(database, helpId)
  const current = ensureParticipant(session)
  const message: RescueMessage = {
    id: `mock-msg-${Date.now()}`,
    seq: nextSeq(session),
    kind: 'text',
    senderOpenId: current.openId,
    senderName: current.displayName,
    text: trimmed,
    imageUrl: null,
    eventType: null,
    action: null,
    createdAt: new Date().toISOString(),
  }
  session.messages.push(message)
  current.lastSeenAt = message.createdAt
  saveDatabase(database)
  return message
}

export function postMockRescueImageMessage(helpId: string, imageUrl: string): RescueMessage {
  const database = loadDatabase()
  const session = ensureSession(database, helpId)
  const current = ensureParticipant(session)
  const message: RescueMessage = {
    id: `mock-msg-${Date.now()}`,
    seq: nextSeq(session),
    kind: 'image',
    senderOpenId: current.openId,
    senderName: current.displayName,
    text: null,
    imageUrl,
    eventType: null,
    action: null,
    createdAt: new Date().toISOString(),
  }
  session.messages.push(message)
  current.lastSeenAt = message.createdAt
  saveDatabase(database)
  return message
}

export function postMockRescueStatusEvent(
  helpId: string,
  eventType: RescueMutableEventType,
  action: RescueEventAction,
): RescueStatusEventResult {
  const database = loadDatabase()
  const session = ensureSession(database, helpId)
  const current = ensureParticipant(session)
  const myState = () => ({
    openId: current.openId,
    takenAed: isMarked(session, current.openId, 'taken_aed'),
    arrived: isMarked(session, current.openId, 'arrived'),
  })
  const alreadyApplied = isMarked(session, current.openId, eventType) === (action === 'mark')
  if (alreadyApplied) return { appended: false, event: null, me: myState() }

  const event = createEventMessage({
    seq: nextSeq(session),
    senderOpenId: current.openId,
    senderName: current.displayName,
    eventType,
    action,
    at: Date.now(),
  })
  session.messages.push(event)
  current.lastSeenAt = event.createdAt
  saveDatabase(database)
  return { appended: true, event, me: myState() }
}

export function reportMockRescueLocation(helpId: string, point: GeoPoint): { reportedAt: string } {
  const database = loadDatabase()
  const session = ensureSession(database, helpId)
  const current = ensureParticipant(session)
  const now = new Date().toISOString()
  current.lastLat = point.lat
  current.lastLng = point.lng
  current.locationReportedAt = now
  current.lastSeenAt = now
  saveDatabase(database)
  return { reportedAt: now }
}

export function fetchMockRescueView(helpId: string): RescueView {
  const database = loadDatabase()
  return toRescueView(ensureSession(database, helpId))
}

export function listMockMineRescueSessions(): RescueMineItem[] {
  return loadDatabase()
    .sessions.filter((session) =>
      ACTIVE_HELP_STATUSES.some((status) => status === session.helpStatus),
    )
    .map((session) => {
      const current = session.participants.find((item) => item.openId === MOCK_CURRENT_OPENID)
      return toMineItem(session, current)
    })
}
