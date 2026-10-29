// 共享站点配置适配层 —— 唯一手工维护真源是仓库根 config/site.json。
//
// 本文件不做第二份配置：它只负责把根 JSON 校验成运行时可用的只读结构。
// 配置在构建时生效；修改 config/site.json 后需要重新构建小程序。
// 非法配置（schemaVersion 不符、校区 code 重复/为空、坐标非法、服务半径非正数）
// 会在模块加载阶段抛出 SiteConfigError，构建/启动即失败，不静默回退旧站点常量。
import rawSiteConfig from '../../config/site.json'
import { calculateDistance } from '@/services/geo-distance'
import type { GeoPoint } from '@/types/common'

/** 校区 code 由 config/site.json 派生，不是源码枚举；新增校区只改配置。 */
export type CampusCode = string

export interface SiteCampus {
  readonly code: CampusCode
  readonly name: string
  readonly center: GeoPoint
}

export interface SiteConfig {
  readonly schemaVersion: number
  readonly organizationName: string
  readonly appTitle: string
  readonly adminTitle: string
  readonly coordinateSystem: string
  readonly mapCenter: GeoPoint
  readonly searchCity: string
  readonly campuses: readonly SiteCampus[]
  readonly serviceRadiusKm: number
  readonly rescuePhone: string
}

export class SiteConfigError extends Error {
  constructor(message: string) {
    super(`[site-config] ${message}`)
    this.name = 'SiteConfigError'
  }
}

const SUPPORTED_SCHEMA_VERSION = 1
const CAMPUS_CODE_PATTERN = /^[a-z0-9-]+$/

interface RawCampus {
  readonly code?: unknown
  readonly name?: unknown
  readonly center?: RawPoint
}

interface RawPoint {
  readonly latitude?: unknown
  readonly longitude?: unknown
}

function requireRecord(value: unknown, label: string): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new SiteConfigError(`${label} 必须是对象`)
  }
  return value as Record<string, unknown>
}

function requireText(value: unknown, label: string): string {
  if (typeof value !== 'string' || !value.trim()) {
    throw new SiteConfigError(`${label} 必须是非空字符串`)
  }
  return value.trim()
}

function requirePoint(value: unknown, label: string): GeoPoint {
  const point = requireRecord(value, label)
  const latitude = point.latitude
  const longitude = point.longitude

  if (typeof latitude !== 'number' || !Number.isFinite(latitude) || Math.abs(latitude) > 90) {
    throw new SiteConfigError(`${label}.latitude 必须是 [-90, 90] 内的有限数字`)
  }
  if (typeof longitude !== 'number' || !Number.isFinite(longitude) || Math.abs(longitude) > 180) {
    throw new SiteConfigError(`${label}.longitude 必须是 [-180, 180] 内的有限数字`)
  }

  return { lat: latitude, lng: longitude }
}

function resolveCampuses(value: unknown): readonly SiteCampus[] {
  if (!Array.isArray(value) || value.length === 0) {
    throw new SiteConfigError('site.campuses 至少需要一个校区')
  }

  const seen = new Set<string>()
  return value.map((rawCampus, index) => {
    const campus = requireRecord(rawCampus, `site.campuses[${index}]`) as RawCampus
    const code = requireText(campus.code, `site.campuses[${index}].code`)
    if (!CAMPUS_CODE_PATTERN.test(code)) {
      throw new SiteConfigError(
        `site.campuses[${index}].code "${code}" 只能包含小写字母、数字和短横线`,
      )
    }
    if (seen.has(code)) {
      throw new SiteConfigError(`site.campuses 存在重复 code "${code}"`)
    }
    seen.add(code)

    return {
      code,
      name: requireText(campus.name, `site.campuses[${index}].name`),
      center: requirePoint(campus.center, `site.campuses[${index}].center`),
    }
  })
}

/** 校验原始 JSON（导出以便测试非法配置的失败路径）。 */
export function resolveSiteConfig(raw: unknown): SiteConfig {
  const root = requireRecord(raw, 'config/site.json')
  const schemaVersion = root.schemaVersion

  if (schemaVersion !== SUPPORTED_SCHEMA_VERSION) {
    throw new SiteConfigError(
      `schemaVersion 必须为 ${SUPPORTED_SCHEMA_VERSION}，实际为 ${String(schemaVersion)}`,
    )
  }

  const site = requireRecord(root.site, 'site')
  const map = requireRecord(site.map, 'site.map')
  const serviceRadiusKm = site.serviceRadiusKm

  if (
    typeof serviceRadiusKm !== 'number'
    || !Number.isFinite(serviceRadiusKm)
    || serviceRadiusKm <= 0
  ) {
    throw new SiteConfigError('site.serviceRadiusKm 必须是正数')
  }

  const rescuePhone = site.rescuePhone
  if (typeof rescuePhone !== 'string') {
    throw new SiteConfigError('site.rescuePhone 必须是字符串（留空表示不提供热线）')
  }

  return {
    schemaVersion: SUPPORTED_SCHEMA_VERSION,
    organizationName: requireText(site.organizationName, 'site.organizationName'),
    appTitle: requireText(site.appTitle, 'site.appTitle'),
    adminTitle: requireText(site.adminTitle, 'site.adminTitle'),
    coordinateSystem: requireText(site.coordinateSystem, 'site.coordinateSystem'),
    mapCenter: requirePoint(map.center, 'site.map.center'),
    searchCity: requireText(map.searchCity, 'site.map.searchCity'),
    campuses: resolveCampuses(site.campuses),
    serviceRadiusKm,
    rescuePhone: rescuePhone.trim(),
  }
}

export const SITE_CONFIG: Readonly<SiteConfig> = resolveSiteConfig(rawSiteConfig)

export const ORGANIZATION_NAME = SITE_CONFIG.organizationName
export const APP_TITLE = SITE_CONFIG.appTitle
export const ADMIN_TITLE = SITE_CONFIG.adminTitle
export const MAP_CENTER: GeoPoint = SITE_CONFIG.mapCenter
export const SEARCH_CITY = SITE_CONFIG.searchCity
export const CAMPUSES: readonly SiteCampus[] = SITE_CONFIG.campuses
export const SERVICE_RADIUS_KM = SITE_CONFIG.serviceRadiusKm
export const SERVICE_RADIUS_METERS = Math.round(SERVICE_RADIUS_KM * 1000)
export const RESCUE_PHONE = SITE_CONFIG.rescuePhone

/** 未指定校区时使用的默认校区（配置数组第一项，顺序即稳定优先级）。 */
export const DEFAULT_CAMPUS_CODE: CampusCode = CAMPUSES[0].code

export function findCampus(code: string): SiteCampus | null {
  return CAMPUSES.find((campus) => campus.code === code) ?? null
}

/** 未知 code 必须显式失败，不能静默落入某个旧校区。 */
export function requireCampus(code: string): SiteCampus {
  const campus = findCampus(code)
  if (!campus) {
    throw new SiteConfigError(
      `未知校区 code "${code}"，可选值：${CAMPUSES.map((item) => item.code).join(', ')}`,
    )
  }
  return campus
}

/** 按配置数组顺序取最近校区；同距时稳定选择靠前的一项。 */
export function resolveNearestCampus(point: GeoPoint): {
  readonly campus: SiteCampus
  readonly distanceMeters: number
} {
  let nearest: SiteCampus = CAMPUSES[0]
  let nearestDistance = calculateDistance(point, nearest.center)

  for (const campus of CAMPUSES.slice(1)) {
    const distance = calculateDistance(point, campus.center)
    if (distance < nearestDistance) {
      nearest = campus
      nearestDistance = distance
    }
  }

  return { campus: nearest, distanceMeters: Math.round(nearestDistance) }
}

/** 距离超出服务半径采样（米）：严格大于才超出，等于边界不判离线。 */
export function isOutsideServiceRadius(distanceMeters: number): boolean {
  return distanceMeters > SERVICE_RADIUS_METERS
}
