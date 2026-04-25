<template>
  <view class="st-root" :class="themeClass">
    <custom-navbar
      title="设置"
      :show-back="true"
      :back-icon="BACK_ICON"
    />

    <view class="st-body">
      <view class="st-group">
        <view class="st-row">
          <text class="st-label">消息通知</text>
          <switch
            :checked="settingsStore.notificationEnabled"
            color="#E53935"
            @change="handleNotificationChange"
          />
        </view>
        <view class="st-divider" />
        <view class="st-row st-row--link" @tap="handleOpenLocationSetting">
          <text class="st-label">位置权限</text>
          <text class="st-arrow">›</text>
        </view>
        <view class="st-divider" />
        <view class="st-row st-row--link" @tap="handleClearCache">
          <text class="st-label">清除缓存</text>
          <view class="st-row-trail">
            <text class="st-sub">{{ cacheSize }}</text>
            <text class="st-arrow">›</text>
          </view>
        </view>
      </view>

      <view class="st-group">
        <!-- 深色模式 -->
        <view class="st-row">
          <text class="st-label">深色模式</text>
          <switch
            :checked="settingsStore.darkMode"
            color="#E53935"
            @change="handleDarkModeChange"
          />
        </view>
        <view class="st-divider" />
        <view class="st-row">
          <text class="st-label">大字模式</text>
          <switch
            :checked="settingsStore.fontScale === 1.2"
            color="#E53935"
            @change="handleFontScaleChange"
          />
        </view>
        <view class="st-divider" />
        <view class="st-row">
          <text class="st-label">减弱动画效果</text>
          <switch
            :checked="settingsStore.reducedMotion"
            color="#E53935"
            @change="handleReducedMotionChange"
          />
        </view>
      </view>

      <view class="st-logout" @tap="handleLogout">
        <text class="st-logout-text">退出登录</text>
      </view>

      <view class="st-version">
        <text class="st-version-text">v1.0.0</text>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
/**
 * 设置页
 *
 * 消息通知 / 位置权限 / 清除缓存 / 深色模式 / 大字模式 / 减弱动画 / 退出登录
 * 本页仅操作本地 Storage，无后端接口依赖。
 * 深色模式通过 CSS 自定义属性全局生效，本页开关即时同步。
 *
 * 设计来源：pages-sub/profile/settings/page-Instrument.md
 */
import { ref } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import CustomNavbar from '@/components/custom-navbar/index.vue'
import { useSettingsStore } from '@/store/settings'
import { useAuthStore } from '@/store/auth'
import { useTheme } from '@/composables/useTheme'
import {
  TOKEN_STORAGE_KEY,
  TOKEN_EXPIRE_KEY,
  CURRENT_USER_ID_STORAGE_KEY,
} from '@/utils/constants'

const BACK_ICON = '/static/icons/ico-left-arrow.svg'

// 与 store/settings.ts STORAGE_PREFIX 同值，仅供本页缓存白名单过滤使用
const SETTINGS_STORAGE_PREFIX = 'aed_settings_'

const PRESERVED_KEYS = new Set([
  TOKEN_STORAGE_KEY,
  TOKEN_EXPIRE_KEY,
  CURRENT_USER_ID_STORAGE_KEY,
])

const settingsStore = useSettingsStore()
const authStore = useAuthStore()
const { themeClass } = useTheme()

const cacheSize = ref('')

function isPreservedKey(key: string): boolean {
  return PRESERVED_KEYS.has(key) || key.startsWith(SETTINGS_STORAGE_PREFIX)
}

function formatCacheSize(kb: number): string {
  if (kb < 1024) return `${kb}KB`
  return `${(kb / 1024).toFixed(1)}MB`
}

function refreshCacheSize(): void {
  try {
    const { currentSize } = uni.getStorageInfoSync()
    cacheSize.value = formatCacheSize(currentSize)
  } catch {
    cacheSize.value = ''
  }
}

/**
 * 从 uni-app switch @change 事件中提取布尔值。
 * vue-tsc 将 <switch> 回退为 SVG 元素类型，故签名使用 Event，内部 cast 取 detail.value。
 */
function getSwitchValue(e: Event): boolean {
  return (e as Event & { detail: { value: boolean } }).detail.value
}

function handleNotificationChange(e: Event): void {
  settingsStore.setNotification(getSwitchValue(e))
}

function handleDarkModeChange(e: Event): void {
  settingsStore.setDarkMode(getSwitchValue(e))
}

function handleFontScaleChange(e: Event): void {
  settingsStore.setFontScale(getSwitchValue(e) ? 1.2 : 1)
}

function handleReducedMotionChange(e: Event): void {
  settingsStore.setReducedMotion(getSwitchValue(e))
}

function handleOpenLocationSetting(): void {
  uni.openSetting({
    success: () => refreshCacheSize(),
  })
}

function handleClearCache(): void {
  const prevSize = cacheSize.value
  uni.showModal({
    title: '清除缓存',
    content: '确定清除所有缓存？清除后需重新加载数据。',
    confirmText: '确定',
    cancelText: '取消',
    success: (res) => {
      if (!res.confirm) return
      try {
        const { keys } = uni.getStorageInfoSync()
        keys.forEach((key) => {
          if (!isPreservedKey(key)) {
            uni.removeStorageSync(key)
          }
        })
      } catch (error) {
        console.error('[Settings] 清除缓存失败', error)
        uni.showToast({ title: '清除失败', icon: 'none' })
        refreshCacheSize()
        return
      }
      const label = prevSize ? `已清除 ${prevSize} 缓存` : '缓存已清除'
      uni.showToast({ title: label, icon: 'none' })
      refreshCacheSize()
    },
  })
}

function handleLogout(): void {
  uni.showModal({
    title: '退出登录',
    content: '确定退出当前账号？',
    confirmText: '确定',
    cancelText: '取消',
    success: (res) => {
      if (!res.confirm) return
      authStore.logout()
      uni.reLaunch({ url: '/pages/map-home/index' })
    },
  })
}

onShow(() => {
  refreshCacheSize()
})
</script>

<style lang="scss" scoped>
.st-root {
  min-height: 100vh;
  background: $background;
}

.st-body {
  padding-top: $spacing-sm;
}

.st-group {
  background: $background-white;
  border-radius: $radius-lg;
  margin: $spacing-md;
  overflow: hidden;
}

.st-row {
  display: flex;
  align-items: center;
  height: 100rpx;
  padding: 0 $spacing-md;
}

.st-label {
  flex: 1;
  font-size: 30rpx;
  color: $text-primary;
}

.st-row-trail {
  display: flex;
  align-items: center;
  gap: $spacing-xs;
}

.st-sub {
  font-size: 26rpx;
  color: $text-tertiary;
}

.st-arrow {
  font-size: 24rpx;
  color: $text-disabled;
}

.st-divider {
  height: 1rpx;
  background: $divider-light;
  margin: 0 $spacing-md;
}

.st-logout {
  margin: 48rpx $spacing-md 0;
  height: 96rpx;
  display: flex;
  align-items: center;
  justify-content: center;
}

.st-logout-text {
  font-size: 30rpx;
  color: $primary;
}

.st-version {
  display: flex;
  justify-content: center;
  padding: $spacing-sm 0 32rpx;
}

.st-version-text {
  font-size: 22rpx;
  color: $text-disabled;
}
</style>
