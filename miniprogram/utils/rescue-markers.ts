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
}

export const SOS_MARKER_ID = 9000
export const PARTICIPANT_MARKER_OFFSET = 2000
export const VIEW_PARTICIPANT_MARKER_OFFSET = 3000

const SOS_ICON = '/static/images/emergency-call.png'
const PARTICIPANT_ICON = '/static/images/avatar-default.png'
const TAKEN_AED_ICON = '/static/images/aed-marker-online.png'
const MARKER_CENTER_ANCHOR = { x: 0.5, y: 0.5 } as const
const LOST_SIGNAL_ALPHA = 0.5
const FULL_ALPHA = 1

interface ParticipantMarkerSource {
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
  index: number,
): SessionMapMarker | null {
  return buildMarker(participant, PARTICIPANT_MARKER_OFFSET + index)
}

/** 求助人只读视图的参与者 marker（无 openId，独立 id 段避免与求助点/AED marker 冲突） */
export function buildViewParticipantMarker(
  participant: RescueViewParticipant,
  index: number,
): SessionMapMarker | null {
  return buildMarker(participant, VIEW_PARTICIPANT_MARKER_OFFSET + index)
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
    iconPath: highlighted ? TAKEN_AED_ICON : PARTICIPANT_ICON,
    width: highlighted ? 42 : 34,
    height: highlighted ? 42 : 34,
    anchor: MARKER_CENTER_ANCHOR,
    alpha: participant.signal === 'lost' ? LOST_SIGNAL_ALPHA : FULL_ALPHA,
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
