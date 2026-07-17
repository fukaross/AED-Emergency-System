import type { AedItem } from '@/types/aed'
import type { HelpRequestDetail } from '@/types/help'
import type { MapMarker } from '@/services/map-service'

export const HELP_MARKER_ID = 9000
export const AED_MARKER_OFFSET = 100

export function buildHelpMarker(item: HelpRequestDetail): MapMarker {
  return {
    id: HELP_MARKER_ID,
    latitude: item.lat,
    longitude: item.lng,
    iconPath: '/static/images/emergency-call.png',
    width: 42,
    height: 42,
    anchor: { x: 0.5, y: 0.5 },
  }
}

export function buildAedMarker(aed: AedItem, index: number): MapMarker {
  return {
    id: AED_MARKER_OFFSET + index,
    latitude: aed.lat,
    longitude: aed.lng,
    iconPath: aed.status === 'online'
      ? '/static/images/aed-marker-online.png'
      : '/static/images/aed-marker-offline.png',
    width: 36,
    height: 36,
    anchor: { x: 0.5, y: 0.5 },
  }
}
