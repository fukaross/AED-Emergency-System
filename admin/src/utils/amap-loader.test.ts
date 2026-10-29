import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

vi.mock('@amap/amap-jsapi-loader', () => ({
  default: {
    load: vi.fn(),
  },
}))

describe('amap-loader', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubGlobal('window', {})
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('throws when VITE_AMAP_KEY is missing', async () => {
    vi.stubEnv('VITE_AMAP_KEY', '')
    const { loadAmap } = await import('@/utils/amap-loader')
    expect(() => loadAmap()).toThrow('高德地图 Key 未配置')
  })

  it('returns cached promise on second call', async () => {
    vi.stubEnv('VITE_AMAP_KEY', 'test-key')
    vi.stubEnv('VITE_AMAP_SECURITY_CODE', '')
    const mockLoadResult = { test: true }
    const amapLoader = await import('@amap/amap-jsapi-loader')
    vi.mocked(amapLoader.default.load).mockResolvedValue(mockLoadResult)

    const { loadAmap } = await import('@/utils/amap-loader')
    const p1 = loadAmap()
    const p2 = loadAmap()
    expect(p1).toBe(p2)
  })

  it('sets _AMapSecurityConfig when security code is provided', async () => {
    vi.stubEnv('VITE_AMAP_KEY', 'test-key')
    vi.stubEnv('VITE_AMAP_SECURITY_CODE', 'test-secret')
    const mockLoadResult = { test: true }
    const amapLoader = await import('@amap/amap-jsapi-loader')
    vi.mocked(amapLoader.default.load).mockResolvedValue(mockLoadResult)

    const { loadAmap } = await import('@/utils/amap-loader')
    loadAmap()
    expect(window._AMapSecurityConfig).toEqual({ securityJsCode: 'test-secret' })
  })

  it('does not set _AMapSecurityConfig when security code is empty', async () => {
    vi.stubEnv('VITE_AMAP_KEY', 'test-key')
    vi.stubEnv('VITE_AMAP_SECURITY_CODE', '')
    const mockLoadResult = { test: true }
    const amapLoader = await import('@amap/amap-jsapi-loader')
    vi.mocked(amapLoader.default.load).mockResolvedValue(mockLoadResult)

    const { loadAmap } = await import('@/utils/amap-loader')
    loadAmap()
    expect(window._AMapSecurityConfig).toBeUndefined()
  })
})
