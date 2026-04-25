/**
 * 主题切换 composable
 *
 * 提供 themeClass（CSS 类名）用于页面根元素绑定，
 * 并通过 uni API 同步原生 page 背景和导航栏颜色。
 *
 * 使用方式：每页根元素 :class="themeClass"
 *
 * 深色模式在设置页持久化，本 composable 只读取 store。
 */
import { computed, watch } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { useSettingsStore } from '@/store/settings'

export function useTheme() {
  const settings = useSettingsStore()

  const themeClass = computed(() =>
    settings.darkMode ? 'theme-dark' : 'theme-light'
  )

  function applyPageTheme() {
    const dark = settings.darkMode
    const bg = dark ? '#121926' : '#F5F5F5'
    const fg = dark ? '#ffffff' : '#000000'

    uni.setBackgroundColor({ backgroundColor: bg, backgroundColorTop: bg })
    uni.setNavigationBarColor({ frontColor: fg, backgroundColor: bg })
  }

  onShow(() => applyPageTheme())
  watch(() => settings.darkMode, () => applyPageTheme())

  return { darkMode: settings.darkMode, themeClass }
}
