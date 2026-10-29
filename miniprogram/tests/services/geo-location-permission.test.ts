import { afterEach, describe, expect, it, vi } from 'vitest'

// 真实定位链路只在 wechat 档启用；这里显式按真实定位模式加载被测模块。
vi.mock('@/utils/runtime-env', () => ({
  USE_SYNTHETIC_LOCATION: false,
}))

import { getCurrentLocation } from '@/services/geo-service'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('location permission handling (real-location mode)', () => {
  it('does not claim that every caller applied a fallback', async () => {
    vi.stubGlobal('uni', {
      getDeviceInfo: () => ({ platform: 'windows' }),
      getSetting: vi.fn().mockResolvedValue({ authSetting: { 'scope.userLocation': false } }),
      authorize: vi.fn(),
      showModal: vi.fn(),
      openSetting: vi.fn(),
    })

    const result = await getCurrentLocation()

    expect(result.errorMessage).toBe('定位权限被拒绝，请开启定位权限后重试')
    expect(result.errorMessage).not.toContain('合成位置')
    expect(result.isSynthetic).toBe(false)
  })

  it('shows the settings guide at most once after a persisted denial', async () => {
    const showModal = vi.fn()
    const authorize = vi.fn()
    vi.stubGlobal('uni', {
      getDeviceInfo: () => ({ platform: 'windows' }),
      getSetting: vi.fn().mockResolvedValue({ authSetting: { 'scope.userLocation': false } }),
      authorize,
      showModal,
      openSetting: vi.fn(),
    })

    await getCurrentLocation({ mode: 'interactive' })
    await getCurrentLocation({ mode: 'interactive' })

    expect(authorize).not.toHaveBeenCalled()
    expect(showModal).toHaveBeenCalledTimes(1)
  })

  it('does not log exact coordinates after a successful location request', async () => {
    const logSpy = vi.spyOn(console, 'log').mockImplementation(() => undefined)
    vi.stubGlobal('uni', {
      getDeviceInfo: () => ({ platform: 'windows' }),
      getSetting: vi.fn().mockResolvedValue({ authSetting: { 'scope.userLocation': true } }),
      getLocation: vi.fn().mockResolvedValue({ latitude: 31.2315, longitude: 121.4785 }),
    })

    const result = await getCurrentLocation()

    expect(result.success).toBe(true)
    expect(result.isSynthetic).toBe(false)
    expect(logSpy).not.toHaveBeenCalled()
  })
})
