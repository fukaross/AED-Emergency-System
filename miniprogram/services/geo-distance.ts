import type { GeoPoint } from '@/types/common'

const EARTH_RADIUS_M = 6371000

/** Haversine 公式计算两点间球面距离（米）。 */
export function calculateDistance(a: GeoPoint, b: GeoPoint): number {
  const toRad = (degrees: number) => (degrees * Math.PI) / 180
  const dLat = toRad(b.lat - a.lat)
  const dLng = toRad(b.lng - a.lng)
  const sinLat = Math.sin(dLat / 2)
  const sinLng = Math.sin(dLng / 2)
  const h = sinLat * sinLat
    + Math.cos(toRad(a.lat)) * Math.cos(toRad(b.lat)) * sinLng * sinLng
  return 2 * EARTH_RADIUS_M * Math.asin(Math.sqrt(h))
}

/** 从 AED 列表中找出距离用户最近的在线 AED。 */
export function findNearestOnlineAed<
  T extends { lat: number; lng: number; status: string },
>(aedList: readonly T[], userPoint: GeoPoint): T | null {
  let nearest: T | null = null
  let minDistance = Infinity
  for (const aed of aedList) {
    if (aed.status !== 'online') continue
    const distance = calculateDistance(userPoint, { lat: aed.lat, lng: aed.lng })
    if (distance >= minDistance) continue
    minDistance = distance
    nearest = aed
  }
  return nearest
}
