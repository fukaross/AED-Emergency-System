import { describe, expect, it } from 'vitest'
import {
  APP_MODE,
  AppConfigError,
  IS_DEMO_MODE,
  IS_LOCAL_DEMO,
  IS_WECHAT_MODE,
  MODE_LABEL,
  RESCUE_PHONE_ENABLED,
  USE_MOCK,
  USE_REAL_LOCATION,
  USE_REAL_WECHAT_FLOW,
  USE_SYNTHETIC_LOCATION,
  resolveApiBaseUrl,
  resolveAppMode,
} from '@/utils/runtime-env'

describe('app mode resolution (VITE_APP_MODE)', () => {
  it('supports exactly the three documented modes', () => {
    expect(resolveAppMode('mock')).toBe('mock')
    expect(resolveAppMode('local-demo')).toBe('local-demo')
    expect(resolveAppMode('wechat')).toBe('wechat')
  })

  it('defaults to mock (front-end only) when unset and trims whitespace', () => {
    expect(resolveAppMode(undefined)).toBe('mock')
    expect(resolveAppMode('')).toBe('mock')
    expect(resolveAppMode('  local-demo ')).toBe('local-demo')
  })

  it('throws a visible config error for unknown modes instead of guessing', () => {
    expect(() => resolveAppMode('demo')).toThrow(AppConfigError)
    expect(() => resolveAppMode('production')).toThrow('非法 VITE_APP_MODE')
  })

  it('derives every backend-ready flag from the mode', () => {
    // 测试环境未设置 VITE_APP_MODE，按 mock 档运行
    expect(APP_MODE).toBe('mock')
    expect(USE_MOCK).toBe(true)
    expect(IS_LOCAL_DEMO).toBe(false)
    expect(IS_WECHAT_MODE).toBe(false)
    expect(IS_DEMO_MODE).toBe(true)
    expect(USE_REAL_WECHAT_FLOW).toBe(false)
    expect(USE_REAL_LOCATION).toBe(false)
    expect(USE_SYNTHETIC_LOCATION).toBe(true)
    expect(RESCUE_PHONE_ENABLED).toBe(false)
    expect(MODE_LABEL).toContain('演示')
  })
})

describe('api base url resolution', () => {
  it('allows an empty url only in mock mode', () => {
    expect(resolveApiBaseUrl('mock', '')).toBe('http://127.0.0.1:5150')
    expect(() => resolveApiBaseUrl('local-demo', '')).toThrow('VITE_API_BASE_URL')
    expect(() => resolveApiBaseUrl('wechat', '   ')).toThrow('VITE_API_BASE_URL')
  })

  it('keeps a valid configured url and strips trailing slashes', () => {
    expect(resolveApiBaseUrl('local-demo', 'http://127.0.0.1:5150/')).toBe(
      'http://127.0.0.1:5150',
    )
    expect(resolveApiBaseUrl('wechat', 'https://api.internal.test')).toBe(
      'https://api.internal.test',
    )
  })

  it('rejects placeholder, non-http and malformed urls', () => {
    expect(() => resolveApiBaseUrl('wechat', 'https://api.example.com')).toThrow(
      '示例占位地址',
    )
    expect(() => resolveApiBaseUrl('local-demo', 'ftp://127.0.0.1')).toThrow(
      'http(s)',
    )
    expect(() => resolveApiBaseUrl('local-demo', 'not-a-url')).toThrow('http(s)')
  })
})
