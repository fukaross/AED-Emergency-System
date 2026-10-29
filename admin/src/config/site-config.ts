import rawSiteConfig from '../../../config/site.json'

/** 与 `config/site.json` 的 `coordinateSystem: "gcj02"` 一致：地图使用高德 / GCJ-02 坐标。 */
export interface SiteCoordinate {
  readonly latitude: number
  readonly longitude: number
}

export interface SiteCampus {
  readonly code: string
  readonly name: string
  readonly center: SiteCoordinate
}

/**
 * Admin 读取的站点配置只读映射。
 *
 * 唯一真源是同仓 `config/site.json`。本文件只做类型化、校验与派生查询，
 * 不保存第二份站点内容，也不允许通过环境变量覆盖站点字段。
 */
export interface SiteConfig {
  readonly organizationName: string
  readonly appTitle: string
  readonly adminTitle: string
  readonly mapCenter: SiteCoordinate
  readonly searchCity: string
  readonly campuses: readonly SiteCampus[]
}

const SUPPORTED_SCHEMA_VERSION = 1
const CAMPUS_CODE_PATTERN = /^[a-z0-9-]+$/

function configError(message: string): never {
  throw new Error(`站点配置 config/site.json 无效：${message}`)
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readString(source: Record<string, unknown>, key: string, label: string): string {
  const value = source[key]
  if (typeof value !== 'string' || value.trim() === '') {
    configError(`${label} 必须是非空字符串`)
  }
  return value
}

function readCoordinate(value: unknown, label: string): SiteCoordinate {
  if (!isRecord(value)) {
    configError(`${label} 必须是包含 latitude / longitude 的对象`)
  }
  const { latitude, longitude } = value
  if (typeof latitude !== 'number' || !Number.isFinite(latitude) || latitude < -90 || latitude > 90) {
    configError(`${label}.latitude 必须是 [-90, 90] 范围内的有限数字`)
  }
  if (typeof longitude !== 'number' || !Number.isFinite(longitude) || longitude < -180 || longitude > 180) {
    configError(`${label}.longitude 必须是 [-180, 180] 范围内的有限数字`)
  }
  return Object.freeze({ latitude, longitude })
}

function readCampuses(value: unknown): readonly SiteCampus[] {
  if (!Array.isArray(value) || value.length === 0) {
    configError('site.campuses 必须是非空数组（至少一个校区）')
  }

  const seenCodes = new Set<string>()
  const campuses: SiteCampus[] = []
  for (const [index, item] of value.entries()) {
    if (!isRecord(item)) {
      configError(`site.campuses[${index}] 必须是对象`)
    }
    const code = readString(item, 'code', `site.campuses[${index}].code`)
    if (!CAMPUS_CODE_PATTERN.test(code)) {
      configError(`site.campuses[${index}].code「${code}」只能包含小写字母、数字和短横线`)
    }
    if (seenCodes.has(code)) {
      configError(`site.campuses 存在重复校区 code「${code}」`)
    }
    seenCodes.add(code)
    campuses.push(
      Object.freeze({
        code,
        name: readString(item, 'name', `site.campuses[${index}].name`),
        center: readCoordinate(item.center, `site.campuses[${index}].center`),
      }),
    )
  }
  return Object.freeze(campuses)
}

/**
 * 校验并映射根 `site` 对象。导出给测试使用，生产路径只通过 `siteConfig` 消费。
 * 非法配置（schemaVersion 不匹配、重复校区 code、坐标非法等）直接抛错，
 * 让构建 / 测试明确失败，而不是静默回落到某个旧校区或旧品牌。
 */
export function parseSiteConfig(raw: unknown): SiteConfig {
  if (!isRecord(raw)) {
    configError('根节点必须是对象')
  }
  if (raw.schemaVersion !== SUPPORTED_SCHEMA_VERSION) {
    configError(`schemaVersion 必须为 ${SUPPORTED_SCHEMA_VERSION}`)
  }
  const site = raw.site
  if (!isRecord(site)) {
    configError('根节点缺少 site 对象')
  }
  const map = site.map
  if (!isRecord(map)) {
    configError('site.map 必须是对象')
  }

  return Object.freeze({
    organizationName: readString(site, 'organizationName', 'site.organizationName'),
    appTitle: readString(site, 'appTitle', 'site.appTitle'),
    adminTitle: readString(site, 'adminTitle', 'site.adminTitle'),
    mapCenter: readCoordinate(map.center, 'site.map.center'),
    searchCity: readString(map, 'searchCity', 'site.map.searchCity'),
    campuses: readCampuses(site.campuses),
  })
}

export const siteConfig: SiteConfig = parseSiteConfig(rawSiteConfig)

/** 默认校区：配置数组第一项（新增校区无需改源码，默认值随配置变化）。 */
export const DEFAULT_CAMPUS: SiteCampus = siteConfig.campuses[0]

export function findCampus(code: string | null | undefined): SiteCampus | undefined {
  if (!code) return undefined
  return siteConfig.campuses.find((campus) => campus.code === code)
}

export function isKnownCampus(code: string | null | undefined): boolean {
  return findCampus(code) !== undefined
}

/**
 * 展示用校区名称。
 *
 * 未知 code 返回带原始 code 的明确错误文案，不静默回落到任一默认校区，
 * 对应 TEST-003「非法 code 不静默接受」；空值只用于占位展示。
 */
export function campusName(code: string | null | undefined): string {
  if (!code) return '-'
  const campus = findCampus(code)
  return campus ? campus.name : `未知校区「${code}」`
}
