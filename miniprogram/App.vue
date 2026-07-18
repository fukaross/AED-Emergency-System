<script setup lang="ts">
// AED 急救小程序 应用入口
import { onLaunch, onShow, onHide } from '@dcloudio/uni-app'
import { useSettingsStore } from '@/store/settings'
import { useAuthStore } from '@/store/auth'

useAuthStore()

function setThemeChrome(bg: string, fg: '#ffffff' | '#000000') {
  if (typeof uni.setBackgroundColor === 'function') {
    uni.setBackgroundColor({ backgroundColor: bg, backgroundColorTop: bg })
  }

  if (typeof uni.setNavigationBarColor === 'function') {
    uni.setNavigationBarColor({ frontColor: fg, backgroundColor: bg })
  }
}

function applyGlobalTheme() {
  const store = useSettingsStore()
  const dark = store.darkMode
  const bg = dark ? '#121926' : '#F5F5F5'
  const fg = dark ? '#ffffff' : '#000000'

  setThemeChrome(bg, fg)
}

onLaunch(() => {
  applyGlobalTheme()
  console.log('[AED] App Launch')
})

onShow(() => {
  applyGlobalTheme()
  console.log('[AED] App Show')
})

onHide(() => {
  console.log('[AED] App Hide')
})
</script>

<style lang="scss">
@use './common/styles/theme.scss';
@use './common/styles/reset.scss';
</style>
