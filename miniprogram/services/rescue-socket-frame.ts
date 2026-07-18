import type {
  RescueHelpStatusPayload,
  RescueMessage,
  RescueParticipant,
  RescueSocketEnvelope,
} from '@/types/rescue-session'
import { MESSAGE_TEXT_MAX_LENGTH } from '@/utils/rescue-session-constants'

export type RescueSocketFrameFailure = 'unsupported_data' | 'malformed_json' | 'invalid_envelope'

export type RescueSocketFrameResult =
  | { readonly ok: true; readonly envelope: RescueSocketEnvelope }
  | { readonly ok: false; readonly reason: RescueSocketFrameFailure }

const MESSAGE_KINDS = new Set(['text', 'status_event'])
const EVENT_TYPES = new Set(['joined', 'taken_aed', 'arrived'])
const EVENT_ACTIONS = new Set(['mark', 'revoke'])
const SIGNALS = new Set(['ok', 'lost', 'gone'])
const HELP_STATUSES = new Set([
  'published',
  'responding',
  'pending_review',
  'closed',
  'false_alarm',
])
/** Parses and validates an untrusted websocket frame before it crosses into session state. */
export function parseRescueSocketFrame(data: string | ArrayBuffer): RescueSocketFrameResult {
  if (typeof data !== 'string' || data.length === 0) {
    return { ok: false, reason: 'unsupported_data' }
  }
  let value: unknown
  try {
    value = JSON.parse(data)
  } catch {
    return { ok: false, reason: 'malformed_json' }
  }
  const envelope = parseEnvelope(value)
  return envelope ? { ok: true, envelope } : { ok: false, reason: 'invalid_envelope' }
}

function parseEnvelope(value: unknown): RescueSocketEnvelope | null {
  if (!isRecord(value) || typeof value.type !== 'string') return null
  if (value.type === 'message' && isMessage(value.data)) {
    return { type: 'message', data: value.data }
  }
  if (value.type === 'participant' && isParticipant(value.data)) {
    return { type: 'participant', data: value.data }
  }
  if (value.type === 'help_status' && isHelpStatus(value.data)) {
    return { type: 'help_status', data: value.data }
  }
  return null
}

function isMessage(value: unknown): value is RescueMessage {
  if (!isRecord(value)) return false
  if (!isNonEmptyString(value.id) || !isPositiveSequence(value.seq)) return false
  if (!isSetValue(value.kind, MESSAGE_KINDS)) return false
  if (!isNonEmptyString(value.senderOpenId) || typeof value.senderName !== 'string') return false
  if (!isNullableString(value.text) || !isNullableSetValue(value.eventType, EVENT_TYPES))
    return false
  if (!isNullableSetValue(value.action, EVENT_ACTIONS) || !isTimestamp(value.createdAt))
    return false
  return value.kind === 'text' ? isValidTextMessage(value) : isValidStatusMessage(value)
}

function isValidTextMessage(value: Record<string, unknown>): boolean {
  return (
    typeof value.text === 'string' &&
    value.text.length > 0 &&
    value.text.length <= MESSAGE_TEXT_MAX_LENGTH &&
    value.eventType === null &&
    value.action === null
  )
}

function isValidStatusMessage(value: Record<string, unknown>): boolean {
  if (value.text !== null || !isSetValue(value.eventType, EVENT_TYPES)) return false
  if (!isSetValue(value.action, EVENT_ACTIONS)) return false
  return value.eventType !== 'joined' || value.action === 'mark'
}

function isParticipant(value: unknown): value is RescueParticipant {
  if (!isRecord(value)) return false
  if (!isNonEmptyString(value.openId) || typeof value.displayName !== 'string') return false
  if (!isTimestamp(value.joinedAt) || typeof value.takenAed !== 'boolean') return false
  if (typeof value.arrived !== 'boolean' || !isSetValue(value.signal, SIGNALS)) return false
  return value.location === null || isLocation(value.location)
}

function isLocation(value: unknown): boolean {
  return (
    isRecord(value) &&
    isCoordinate(value.lat, -90, 90) &&
    isCoordinate(value.lng, -180, 180) &&
    isTimestamp(value.reportedAt)
  )
}

function isHelpStatus(value: unknown): value is RescueHelpStatusPayload {
  return isRecord(value) && isSetValue(value.helpStatus, HELP_STATUSES)
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.length > 0
}

function isNullableString(value: unknown): value is string | null {
  return value === null || typeof value === 'string'
}

function isTimestamp(value: unknown): value is string {
  return typeof value === 'string' && Number.isFinite(Date.parse(value))
}

function isPositiveSequence(value: unknown): value is number {
  return Number.isSafeInteger(value) && Number(value) > 0
}

function isCoordinate(value: unknown, min: number, max: number): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value >= min && value <= max
}

function isSetValue(value: unknown, values: ReadonlySet<string>): value is string {
  return typeof value === 'string' && values.has(value)
}

function isNullableSetValue(value: unknown, values: ReadonlySet<string>): boolean {
  return value === null || isSetValue(value, values)
}
