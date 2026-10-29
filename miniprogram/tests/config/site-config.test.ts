import { describe, expect, it } from 'vitest'
import {
  CAMPUSES,
  MAP_CENTER,
  RESCUE_PHONE,
  SEARCH_CITY,
  SERVICE_RADIUS_KM,
  SERVICE_RADIUS_METERS,
  SITE_CONFIG,
  SiteConfigError,
  findCampus,
  isOutsideServiceRadius,
  requireCampus,
  resolveNearestCampus,
  resolveSiteConfig,
} from '@/config/site-config'

function buildRawSiteConfig(overrides: Record<string, unknown> = {}) {
  return {
    schemaVersion: 1,
    site: {
      organizationName: '测试机构',
      appTitle: '测试标题',
      adminTitle: '测试管理端',
      coordinateSystem: 'gcj02',
      map: {
        center: { latitude: 30, longitude: 120 },
        searchCity: '测试城市',
      },
      campuses: [
        { code: 'alpha', name: '甲区', center: { latitude: 30, longitude: 120 } },
        { code: 'beta', name: '乙区', center: { latitude: 30.1, longitude: 120.1 } },
      ],
      serviceRadiusKm: 12,
      rescuePhone: '',
      ...overrides,
    },
  }
}

describe('site config adapter (config/site.json is the only source)', () => {
  it('loads the repository root config', () => {
    expect(SITE_CONFIG.schemaVersion).toBe(1)
    expect(SITE_CONFIG.organizationName).toBe('示范园区')
    expect(CAMPUSES.map((campus) => campus.code)).toEqual(['east', 'west'])
    expect(SEARCH_CITY).toBe('上海')
    expect(MAP_CENTER).toEqual({ lat: 31.2315, lng: 121.4785 })
    expect(SERVICE_RADIUS_KM).toBe(30)
    expect(SERVICE_RADIUS_METERS).toBe(30000)
    expect(RESCUE_PHONE).toBe('')
  })

  it('accepts a new campus without touching any source enum', () => {
    const raw = buildRawSiteConfig()
    raw.site.campuses.push({
      code: 'gamma',
      name: '丙区',
      center: { latitude: 30.2, longitude: 120.2 },
    })

    const config = resolveSiteConfig(raw)

    expect(config.campuses).toHaveLength(3)
    expect(config.campuses[2]).toMatchObject({ code: 'gamma', name: '丙区' })
    // 真实全局配置仍只有两个校区；未知 code 不改写为默认校区
    expect(findCampus('gamma')).toBeNull()
    expect(() => requireCampus('gamma')).toThrow(SiteConfigError)
  })

  it('resolves the nearest campus by config order and reports metres', () => {
    const nearest = resolveNearestCampus({ lat: 31.218, lng: 121.452 })

    expect(nearest.campus.code).toBe('west')
    expect(nearest.distanceMeters).toBe(0)
  })

  it('does not fall back to a default campus for unknown codes', () => {
    expect(() => requireCampus('legacy-campus')).toThrow('未知校区 code')
  })

  it('rejects duplicate campus codes', () => {
    const raw = buildRawSiteConfig({
      campuses: [
        { code: 'dup', name: '甲区', center: { latitude: 30, longitude: 120 } },
        { code: 'dup', name: '乙区', center: { latitude: 30.1, longitude: 120.1 } },
      ],
    })

    expect(() => resolveSiteConfig(raw)).toThrow('重复 code')
  })

  it('rejects invalid coordinates, empty campus list and bad radius', () => {
    const badCoordinate = buildRawSiteConfig({
      campuses: [{ code: 'a', name: '甲区', center: { latitude: 200, longitude: 120 } }],
    })
    const emptyCampuses = buildRawSiteConfig({ campuses: [] })
    const zeroRadius = buildRawSiteConfig({ serviceRadiusKm: 0 })

    expect(() => resolveSiteConfig(badCoordinate)).toThrow('latitude')
    expect(() => resolveSiteConfig(emptyCampuses)).toThrow('至少需要一个校区')
    expect(() => resolveSiteConfig(zeroRadius)).toThrow('serviceRadiusKm')
  })

  it('rejects unsupported schema versions', () => {
    expect(() => resolveSiteConfig({ ...buildRawSiteConfig(), schemaVersion: 2 })).toThrow(
      'schemaVersion',
    )
  })

  it('treats the exact service-radius boundary as inside', () => {
    expect(isOutsideServiceRadius(SERVICE_RADIUS_METERS)).toBe(false)
    expect(isOutsideServiceRadius(SERVICE_RADIUS_METERS + 1)).toBe(true)
  })
})
