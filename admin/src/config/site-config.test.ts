import { describe, expect, it } from 'vitest'
import rawSiteConfig from '../../../config/site.json'
import {
  DEFAULT_CAMPUS,
  campusName,
  findCampus,
  isKnownCampus,
  parseSiteConfig,
  siteConfig,
} from './site-config'

function rawSiteWith(overrides: Record<string, unknown>) {
  return { schemaVersion: 1, site: { ...structuredClone(rawSiteConfig.site), ...overrides } }
}

describe('site-config 映射（唯一真源 config/site.json）', () => {
  it('机构名与标题来自配置，不是在 Admin 内维护第二份配置', () => {
    expect(siteConfig.organizationName).toBe(rawSiteConfig.site.organizationName)
    expect(siteConfig.appTitle).toBe(rawSiteConfig.site.appTitle)
    expect(siteConfig.adminTitle).toBe(rawSiteConfig.site.adminTitle)
  })

  it('地图中心与检索城市来自配置', () => {
    expect(siteConfig.mapCenter).toEqual(rawSiteConfig.site.map.center)
    expect(siteConfig.searchCity).toBe(rawSiteConfig.site.map.searchCity)
  })

  it('校区列表逐项来自配置，默认校区是配置数组第一项', () => {
    const configuredCodes = rawSiteConfig.site.campuses.map((campus) => campus.code)
    const configuredNames = rawSiteConfig.site.campuses.map((campus) => campus.name)

    expect(siteConfig.campuses.map((campus) => campus.code)).toEqual(configuredCodes)
    expect(siteConfig.campuses.map((campus) => campus.name)).toEqual(configuredNames)
    expect(DEFAULT_CAMPUS.code).toBe(configuredCodes[0])
    expect(findCampus(DEFAULT_CAMPUS.code)?.name).toBe(DEFAULT_CAMPUS.name)
    expect(isKnownCampus(DEFAULT_CAMPUS.code)).toBe(true)
  })

  it('未知校区 code 返回带 code 的可见错误文案，不静默回落到默认校区', () => {
    expect(isKnownCampus('not-configured')).toBe(false)
    expect(campusName('not-configured')).toBe('未知校区「not-configured」')
    expect(campusName('')).toBe('-')
    expect(campusName(null)).toBe('-')
  })
})

describe('site-config 非法配置清晰失败', () => {
  it('拒绝不支持的 schemaVersion', () => {
    expect(() => parseSiteConfig({ ...structuredClone(rawSiteConfig), schemaVersion: 2 }))
      .toThrow(/schemaVersion/)
  })

  it('拒绝重复校区 code', () => {
    const duplicated = [rawSiteConfig.site.campuses[0], rawSiteConfig.site.campuses[0]]
    expect(() => parseSiteConfig(rawSiteWith({ campuses: duplicated })))
      .toThrow(/重复校区 code/)
  })

  it('拒绝非法坐标（校区中心与地图中心同为校验口径）', () => {
    const invalidCampus = [
      { code: 'east', name: '东区', center: { latitude: 91, longitude: 121.5 } },
    ]
    expect(() => parseSiteConfig(rawSiteWith({ campuses: invalidCampus }))).toThrow(/latitude/)

    const invalidMap = { center: { latitude: 31.2, longitude: 181 }, searchCity: '上海' }
    expect(() => parseSiteConfig(rawSiteWith({ map: invalidMap }))).toThrow(/longitude/)
  })

  it('拒绝空校区列表与缺失机构名', () => {
    expect(() => parseSiteConfig(rawSiteWith({ campuses: [] }))).toThrow(/campuses/)
    expect(() => parseSiteConfig(rawSiteWith({ organizationName: '  ' })))
      .toThrow(/organizationName/)
  })

  it('拒绝不符合 code 约定的校区（大写字母）', () => {
    const uppercase = [
      { code: 'East', name: '东区', center: { latitude: 31.2, longitude: 121.5 } },
    ]
    expect(() => parseSiteConfig(rawSiteWith({ campuses: uppercase }))).toThrow(/小写字母/)
  })
})
