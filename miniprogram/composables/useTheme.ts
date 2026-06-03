// 主题切换 —— 从 settings store 读当前主题，同步原生导航栏颜色
import { computed, watch } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { useSettingsStore } from '@/store/settings'

function setPageThemeChrome(bg: string, fg: '#ffffff' | '#000000'): void {
  if (typeof uni.setBackgroundColor === 'function') {
    uni.setBackgroundColor({ backgroundColor: bg, backgroundColorTop: bg })
  }

  if (typeof uni.setNavigationBarColor === 'function') {
    uni.setNavigationBarColor({ frontColor: fg, backgroundColor: bg })
  }
}

export function useTheme() {
  const settings = useSettingsStore()

  const themeClass = computed(() =>
    settings.darkMode ? 'theme-dark' : 'theme-light'
  )

  function applyPageTheme() {
    const dark = settings.darkMode
    const bg = dark ? '#121926' : '#F5F5F5'
    const fg = dark ? '#ffffff' : '#000000'

    setPageThemeChrome(bg, fg)
  }

  onShow(() => applyPageTheme())
  watch(() => settings.darkMode, () => applyPageTheme())

  return { darkMode: settings.darkMode, themeClass }
}
