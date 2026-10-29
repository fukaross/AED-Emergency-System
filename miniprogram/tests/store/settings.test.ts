import { describe, it, expect, beforeEach, vi } from 'vitest'

// Mock uni API for settings store test
vi.stubGlobal('uni', {
  getStorageSync: vi.fn(() => undefined),
  setStorageSync: vi.fn(),
  removeStorageSync: vi.fn(),
})

// Must import after uni mock
import { createPinia, setActivePinia } from 'pinia'
import { useSettingsStore } from '@/store/settings'

describe('settings store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('has correct default values', () => {
    const store = useSettingsStore()
    expect(store.notificationEnabled).toBe(true)
    expect(store.darkMode).toBe(false)
    expect(store.fontScale).toBe(1)
    expect(store.reducedMotion).toBe(false)
  })

  it('toggles dark mode', () => {
    const store = useSettingsStore()
    store.setDarkMode(true)
    expect(store.darkMode).toBe(true)
  })

  it('sets font scale', () => {
    const store = useSettingsStore()
    store.setFontScale(1.2)
    expect(store.fontScale).toBe(1.2)
  })

  it('sets reduced motion', () => {
    const store = useSettingsStore()
    store.setReducedMotion(true)
    expect(store.reducedMotion).toBe(true)
  })

  it('loads settings from storage on init', () => {
    const mockStorage: Record<string, unknown> = {
      'aed_settings_darkMode': true,
      'aed_settings_fontScale': 1.2,
    }
    vi.mocked(uni.getStorageSync).mockImplementation((key: string) => mockStorage[key])

    // Re-create store after mocking storage — values load in constructor
    const store = useSettingsStore()
    // Force re-init by accessing the store (pinia caches, so we verify defaults
    // were overridden during construction)
    expect(store.darkMode).toBe(true)
    expect(store.fontScale).toBe(1.2)
  })

  it('parses string boolean values from storage', () => {
    const mockStorage: Record<string, unknown> = {
      'aed_settings_notification': 'false',
      'aed_settings_darkMode': 'false',
      'aed_settings_reducedMotion': 'true',
    }
    vi.mocked(uni.getStorageSync).mockImplementation((key: string) => mockStorage[key])

    const store = useSettingsStore()

    expect(store.notificationEnabled).toBe(false)
    expect(store.darkMode).toBe(false)
    expect(store.reducedMotion).toBe(true)
  })
})
