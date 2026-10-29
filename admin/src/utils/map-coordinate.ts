import { siteConfig } from '@/config/site-config'

/** 无效坐标时的兜底视图中心，同样来自共享站点配置，不维护第二份坐标。 */
export const ADMIN_MAP_CENTER = {
  lat: siteConfig.mapCenter.latitude,
  lng: siteConfig.mapCenter.longitude,
}
export const DEFAULT_ZOOM = 17

export function lngLatToPoint(lnglat: [number, number]): { lat: number; lng: number } {
  return { lat: lnglat[1], lng: lnglat[0] }
}

export function pointToLngLat(lat: number, lng: number): [number, number] {
  return [lng, lat]
}

export function isValidCoordinate(lat: number, lng: number): boolean {
  if (Math.abs(lat) < 0.01 && Math.abs(lng) < 0.01) return false
  if (lat < -90 || lat > 90) return false
  if (lng < -180 || lng > 180) return false
  return true
}

export function getDisplayCenter(lat: number, lng: number): [number, number] {
  if (isValidCoordinate(lat, lng)) {
    return pointToLngLat(lat, lng)
  }
  return pointToLngLat(ADMIN_MAP_CENTER.lat, ADMIN_MAP_CENTER.lng)
}
