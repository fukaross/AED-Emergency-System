// 急救会话地图 marker 构造与移动插值 —— 纯函数，便于单测（BackEnd.md §4.5A）
import type { GeoPoint } from '@/types/common'
import type {
  RescueParticipant,
  RescueSos,
  RescueSignal,
  RescueViewParticipant,
} from '@/types/rescue-session'
import type { MapMarker } from '@/services/map-service'

export interface SessionMapMarker extends MapMarker {
  readonly alpha?: number
  readonly callout?: {
    readonly content: string
    readonly color: string
    readonly fontSize: number
    readonly borderRadius: number
    readonly bgColor: string
    readonly padding: number
    readonly display: 'ALWAYS'
  }
}

export const SOS_MARKER_ID = 9000
export const PARTICIPANT_MARKER_OFFSET = 10_000
export const VIEW_PARTICIPANT_MARKER_OFFSET = 1_010_000

const SOS_ICON = '/static/images/emergency-call.png'
const PARTICIPANT_ICON = '/static/images/avatar-default.png'
const MARKER_CENTER_ANCHOR = { x: 0.5, y: 0.5 } as const
const LOST_SIGNAL_ALPHA = 0.5
const FULL_ALPHA = 1
const MARKER_ID_RANGE = 1_000_000
const NORMAL_MARKER_SIZE = 34
const AED_CARRIER_MARKER_SIZE = 42
const CALLOUT_TEXT_COLOR = '#1F2937'
const CALLOUT_BACKGROUND = '#FFFFFF'

interface ParticipantMarkerSource {
  readonly displayName: string
  readonly takenAed: boolean
  readonly signal: RescueSignal
  readonly location: { readonly lat: number; readonly lng: number } | null
}

export function buildSosMarker(sos: RescueSos): SessionMapMarker {
  return {
    id: SOS_MARKER_ID,
    latitude: sos.lat,
    longitude: sos.lng,
    iconPath: SOS_ICON,
    width: 42,
    height: 42,
    anchor: MARKER_CENTER_ANCHOR,
    alpha: FULL_ALPHA,
  }
}

/** 拿 AED 者高亮为 AED 图标；lost 半透明；gone 或无位置返回 null（地图移除） */
export function buildParticipantMarker(
  participant: RescueParticipant,
  _index: number,
): SessionMapMarker | null {
  return buildMarker(participant, stableMarkerId(participant.openId, PARTICIPANT_MARKER_OFFSET))
}

/** 求助人只读视图的参与者 marker（无 openId，独立 id 段避免与求助点/AED marker 冲突） */
export function buildViewParticipantMarker(
  participant: RescueViewParticipant,
  _index: number,
): SessionMapMarker | null {
  return buildMarker(
    participant,
    stableMarkerId(participant.participantId, VIEW_PARTICIPANT_MARKER_OFFSET),
  )
}

function buildMarker(
  participant: ParticipantMarkerSource,
  id: number,
): SessionMapMarker | null {
  if (participant.signal === 'gone' || !participant.location) return null

  const highlighted = participant.takenAed
  return {
    id,
    latitude: participant.location.lat,
    longitude: participant.location.lng,
    iconPath: PARTICIPANT_ICON,
    width: highlighted ? AED_CARRIER_MARKER_SIZE : NORMAL_MARKER_SIZE,
    height: highlighted ? AED_CARRIER_MARKER_SIZE : NORMAL_MARKER_SIZE,
    anchor: MARKER_CENTER_ANCHOR,
    alpha: participant.signal === 'lost' ? LOST_SIGNAL_ALPHA : FULL_ALPHA,
    callout: buildParticipantCallout(participant.displayName, highlighted),
  }
}

function stableMarkerId(identity: string, offset: number): number {
  let hash = 0
  for (const character of identity) {
    hash = (hash * 31 + character.charCodeAt(0)) % MARKER_ID_RANGE
  }
  return offset + hash
}

function buildParticipantCallout(
  displayName: string,
  takenAed: boolean,
): NonNullable<SessionMapMarker['callout']> {
  return {
    content: takenAed ? `${displayName} · 携带 AED` : displayName,
    color: CALLOUT_TEXT_COLOR,
    fontSize: 11,
    borderRadius: 4,
    bgColor: CALLOUT_BACKGROUND,
    padding: 4,
    display: 'ALWAYS',
  }
}

/** 地图平滑移动插值：from → to 按 t∈[0,1] 线性过渡 */
export function interpolateGeo(from: GeoPoint, to: GeoPoint, t: number): GeoPoint {
  const clamped = Math.min(1, Math.max(0, t))
  return {
    lat: from.lat + (to.lat - from.lat) * clamped,
    lng: from.lng + (to.lng - from.lng) * clamped,
  }
}

/** 使用匿名 participantId 对齐前后帧，避免同名参与者串位。 */
export function interpolateViewParticipants(
  current: readonly RescueViewParticipant[],
  target: readonly RescueViewParticipant[],
  progress: number,
): RescueViewParticipant[] {
  const currentById = new Map(current.map((participant) => [
    participant.participantId,
    participant,
  ]))
  return target.map((participant) => interpolateViewParticipant(
    currentById.get(participant.participantId),
    participant,
    progress,
  ))
}

function interpolateViewParticipant(
  current: RescueViewParticipant | undefined,
  target: RescueViewParticipant,
  progress: number,
): RescueViewParticipant {
  if (!current?.location || !target.location) return target
  const point = interpolateGeo(current.location, target.location, progress)
  return {
    ...target,
    location: { ...target.location, ...point },
  }
}
