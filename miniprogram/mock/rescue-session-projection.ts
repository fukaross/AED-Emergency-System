import type {
  RescueEventAction,
  RescueEventType,
  RescueLocation,
  RescueMe,
  RescueMineItem,
  RescueParticipant,
  RescueSessionSnapshot,
  RescueTiming,
  RescueView,
} from '@/types/rescue-session'
import type { MockParticipantRecord, MockSessionRecord } from '@/mock/rescue-session-storage'
import {
  computeSignal,
  RESCUE_LOCATION_REPORT_INTERVAL_MS,
  RESCUE_SIGNAL_GONE_AFTER_SECONDS,
  RESCUE_SIGNAL_LOST_AFTER_SECONDS,
} from '@/utils/rescue-signal'

const MS_PER_SECOND = 1000
const MOCK_RESCUE_TIMING: RescueTiming = {
  signalLostAfterSeconds: RESCUE_SIGNAL_LOST_AFTER_SECONDS,
  locationGoneAfterSeconds: RESCUE_SIGNAL_GONE_AFTER_SECONDS,
  locationReportIntervalSeconds: RESCUE_LOCATION_REPORT_INTERVAL_MS / MS_PER_SECOND,
}

export function nextSeq(session: MockSessionRecord): number {
  return latestSeqOf(session) + 1
}

export function isMarked(
  session: MockSessionRecord,
  openId: string,
  eventType: RescueEventType,
): boolean {
  const latest = session.messages
    .filter(
      (message) =>
        message.kind === 'status_event' &&
        message.senderOpenId === openId &&
        message.eventType === eventType,
    )
    .sort((a, b) => b.seq - a.seq)[0]
  return latest?.action === 'mark'
}

function toLocation(participant: MockParticipantRecord): RescueLocation | null {
  if (participant.lastLat === null || participant.lastLng === null || !participant.locationReportedAt) {
    return null
  }
  return {
    lat: participant.lastLat,
    lng: participant.lastLng,
    reportedAt: participant.locationReportedAt,
  }
}

export function toParticipant(
  record: MockParticipantRecord,
  session: MockSessionRecord,
): RescueParticipant {
  const signal = record.locationReportedAt
    ? computeSignal(record.locationReportedAt, Date.now())
    : 'gone'
  return {
    openId: record.openId,
    displayName: record.displayName,
    avatarUrl: record.avatarUrl,
    joinedAt: record.joinedAt,
    takenAed: isMarked(session, record.openId, 'taken_aed'),
    arrived: isMarked(session, record.openId, 'arrived'),
    signal,
    location: signal === 'gone' ? null : toLocation(record),
  }
}

export function latestSeqOf(session: MockSessionRecord): number {
  return session.messages.reduce((max, message) => Math.max(max, message.seq), 0)
}

export function toSnapshot(
  session: MockSessionRecord,
  current: MockParticipantRecord,
  afterSeq?: number,
): RescueSessionSnapshot {
  const messages = afterSeq === undefined
    ? session.messages
    : session.messages.filter((message) => message.seq > afterSeq)
  const me: RescueMe = {
    openId: current.openId,
    takenAed: isMarked(session, current.openId, 'taken_aed'),
    arrived: isMarked(session, current.openId, 'arrived'),
    lastReadSeq: current.lastReadSeq,
  }
  return {
    helpId: session.helpId,
    requestNo: session.requestNo,
    helpStatus: session.helpStatus,
    sos: session.sos,
    me,
    participants: session.participants.map((item) => toParticipant(item, session)),
    messages,
    latestSeq: latestSeqOf(session),
    serverTime: new Date().toISOString(),
    timing: MOCK_RESCUE_TIMING,
  }
}

export function toRescueView(session: MockSessionRecord): RescueView {
  const participants = session.participants.map((item, index) => {
    const participant = toParticipant(item, session)
    return {
      participantId: `mock-participant-${index + 1}`,
      displayName: participant.displayName,
      takenAed: participant.takenAed,
      arrived: participant.arrived,
      signal: participant.signal,
      location: participant.location,
    }
  })
  return {
    helpId: session.helpId,
    helpStatus: session.helpStatus,
    sos: session.sos,
    respondersCount: participants.length,
    participants,
    events: session.messages
      .filter((message) => message.kind === 'status_event')
      .map((message) => ({
        seq: message.seq,
        eventType: message.eventType as RescueEventType,
        action: message.action as RescueEventAction,
        actorName: message.senderName,
        createdAt: message.createdAt,
      })),
    serverTime: new Date().toISOString(),
    timing: MOCK_RESCUE_TIMING,
  }
}

export function toMineItem(
  session: MockSessionRecord,
  current?: MockParticipantRecord,
): RescueMineItem {
  return {
    helpId: session.helpId,
    requestNo: session.requestNo,
    helpStatus: session.helpStatus,
    type: 'help_hand',
    address: session.sos.address,
    joined: current !== undefined,
    joinedAt: current?.joinedAt ?? null,
    unreadCount: current
      ? session.messages.filter(
        (message) => message.seq > current.lastReadSeq && message.senderOpenId !== current.openId,
      ).length
      : 0,
    lastActivityAt: session.messages.at(-1)?.createdAt ?? null,
  }
}
