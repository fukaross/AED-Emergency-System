// 全局展示设置 —— 通知、主题、字号、动画等
import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

const STORAGE_PREFIX = 'aed_settings_'

function loadBool(key: string, defaultVal: boolean): boolean {
  try {
    const val = uni.getStorageSync(`${STORAGE_PREFIX}${key}`)
    if (val === '' || val === undefined || val === null) return defaultVal
    if (typeof val === 'string') {
      if (val === 'true') return true
      if (val === 'false') return false
      return defaultVal
    }
    return Boolean(val)
  } catch {
    return defaultVal
  }
}

function loadNumber(key: string, defaultVal: number): number {
  try {
    const val = uni.getStorageSync(`${STORAGE_PREFIX}${key}`)
    return val === '' || val === undefined ? defaultVal : Number(val)
  } catch {
    return defaultVal
  }
}

function saveSetting(key: string, value: unknown): void {
  try {
    uni.setStorageSync(`${STORAGE_PREFIX}${key}`, value)
  } catch {
    console.warn(`[Settings] 保存 ${key} 失败`)
  }
}

export const useSettingsStore = defineStore('settings', () => {
  // ===== State =====
  const notificationEnabled = ref(loadBool('notification', true))
  const darkMode = ref(loadBool('darkMode', false))
  const fontScale = ref<1 | 1.2>(loadNumber('fontScale', 1) as 1 | 1.2)
  const reducedMotion = ref(loadBool('reducedMotion', false))

  // ===== 持久化 =====
  watch(notificationEnabled, v => saveSetting('notification', v))
  watch(darkMode, v => saveSetting('darkMode', v))
  watch(fontScale, v => saveSetting('fontScale', v))
  watch(reducedMotion, v => saveSetting('reducedMotion', v))

  // ===== Actions =====
  function setDarkMode(enabled: boolean): void {
    darkMode.value = enabled
  }

  function setFontScale(scale: 1 | 1.2): void {
    fontScale.value = scale
  }

  function setReducedMotion(enabled: boolean): void {
    reducedMotion.value = enabled
  }

  function setNotification(enabled: boolean): void {
    notificationEnabled.value = enabled
  }

  return {
    notificationEnabled,
    darkMode,
    fontScale,
    reducedMotion,
    setDarkMode,
    setFontScale,
    setReducedMotion,
    setNotification,
  }
})
