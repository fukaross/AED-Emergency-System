<template>
  <view class="profile" :class="themeClass">
    <!-- 沉浸式头部 -->
    <view class="pf-header" :style="{ paddingTop: statusBarPx }">
      <image class="pf-header-bg" :src="PROFILE_BG" mode="aspectFill" />
      <view class="pf-header-mask" />

      <!-- 已登录态 -->
      <view v-if="authStore.isLoggedIn" class="pf-user">
        <image
          class="pf-avatar"
          :src="avatarUrl"
          mode="aspectFill"
          @click="changeAvatar"
        />
        <view class="pf-name-row">
          <text class="pf-name">{{ userStore.displayName }}</text>
          <text v-if="genderIcon" class="pf-gender" :class="genderClass">
            {{ genderIcon }}
          </text>
        </view>
        <view class="pf-tags">
          <view class="pf-cert" :class="certClass">
            <image class="pf-cert-icon" :src="certIcon" mode="aspectFit" />
            <text class="pf-cert-text">{{ certText }}</text>
          </view>
          <level-badge
            v-if="userStore.rescuerLevel"
            :level="userStore.rescuerLevel"
          />
        </view>
      </view>

      <!-- 游客态 -->
      <view v-else class="pf-guest">
        <image class="pf-avatar" :src="DEFAULT_AVATAR" mode="aspectFill" />
        <text class="pf-guest-name">游客</text>
        <view class="pf-login-btn" @click="handleLogin">
          <text class="pf-login-text">点击登录</text>
        </view>
      </view>
    </view>

    <!-- 选项列表 -->
    <ProfileMenuList @select="handleMenuClick" />

    <view class="pf-spacer" />

    <AppTabBar current-path="/pages/profile/index" />
  </view>
</template>

<script setup lang="ts">
/**
 * 个人中心页面
 *
 * 沉浸式头部（模糊背景 + 头像 + 用户名 + 认证/级别标签）
 * 选项列表 + 游客态登录引导
 *
 * 设计来源：CLAUDE.md §4.4 + page-Instrument.md
 */
import { computed } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { useTheme } from '@/composables/useTheme'
import { useAuthStore } from '@/store/auth'
import { useUserStore } from '@/store/user'
import { uploadImage } from '@/api/common'
import AppTabBar from '@/components/app-tab-bar/index.vue'
import LevelBadge from '@/components/level-badge/index.vue'
import ProfileMenuList from './components/profile-menu-list.vue'

const PROFILE_BG = '/static/images/profile-bg.png'
const DEFAULT_AVATAR = '/static/images/avatar-default.png'

interface ProfileMenuItem {
  readonly title: string
  readonly url: string
  readonly needAuth: boolean
}

const authStore = useAuthStore()
const userStore = useUserStore()
const { themeClass } = useTheme()

const sysInfo = uni.getSystemInfoSync()
const statusBarPx = `${sysInfo.statusBarHeight ?? 0}px`

// ===== 计算属性 =====
const avatarUrl = computed(() =>
  userStore.userInfo?.avatarUrl || DEFAULT_AVATAR,
)

const genderIcon = computed(() => {
  const g = userStore.userInfo?.gender
  if (g === 'male') return '♂'
  if (g === 'female') return '♀'
  return ''
})

const genderClass = computed(() => {
  const g = userStore.userInfo?.gender
  return g === 'male' ? 'pf-gender--male' : 'pf-gender--female'
})

const certText = computed(() =>
  userStore.isCertified ? '已认证' : '未认证',
)

const certClass = computed(() =>
  userStore.isCertified ? 'pf-cert--yes' : 'pf-cert--no',
)

const certIcon = computed(() =>
  userStore.isCertified
    ? '/static/icons/verified.svg'
    : '/static/icons/unverified.svg',
)

// ===== 操作 =====
function changeAvatar() {
  uni.chooseImage({
    count: 1,
    sizeType: ['compressed'],
    success: async (res) => {
      const path = res.tempFilePaths[0]
      if (path) {
        try {
          const uploaded = await uploadImage(path, 'avatar')
          await userStore.saveProfile({ avatarUrl: uploaded.url })
          uni.showToast({ title: '头像已更新', icon: 'success' })
        } catch {
          uni.showToast({ title: '头像上传失败', icon: 'none' })
        }
      }
    },
  })
}

function handleLogin() {
  authStore.login().catch(() => {
    uni.showToast({ title: '登录失败，请重试', icon: 'none' })
  })
}

function handleMenuClick(item: ProfileMenuItem) {
  if (item.needAuth && !authStore.isLoggedIn) {
    uni.showToast({ title: '请先登录', icon: 'none' })
    return
  }
  if (!item.url) {
    uni.showToast({ title: '功能开发中...', icon: 'none' })
    return
  }
  uni.navigateTo({ url: item.url })
}

// ===== 生命周期 =====
onShow(() => {
  authStore.checkAuth()
  if (authStore.isLoggedIn) {
    userStore.loadProfile().catch(() => {
      uni.showToast({ title: '用户资料加载失败', icon: 'none' })
    })
  }
})
</script>

<style lang="scss" scoped>
.profile {
  min-height: 100vh;
  background: $background;
}

// ===== 头部 =====
.pf-header {
  position: relative;
  padding-bottom: $spacing-xxl;
  overflow: hidden;
}

.pf-header-bg {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  filter: blur(20px);
  transform: scale(1.1);
}

.pf-header-mask {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: linear-gradient(
    to bottom,
    rgba(0, 0, 0, 0.3) 0%,
    rgba(0, 0, 0, 0.3) 65%,
    rgba(245, 245, 245, 0.6) 90%,
    $background 100%
  );
}

// ===== 已登录 =====
.pf-user {
  position: relative;
  @include flex-column;
  align-items: center;
  padding: $spacing-xl 0 $spacing-lg;
}

.pf-avatar {
  width: 120rpx;
  height: 120rpx;
  border-radius: $radius-full;
  border: 4rpx solid $text-white;
}

.pf-name-row {
  @include flex-align-center;
  gap: $spacing-xs;
  margin-top: $spacing-sm;
}

.pf-name {
  font-size: $font-size-xl;
  font-weight: $font-weight-bold;
  color: $text-white;
}

.pf-gender {
  font-size: $font-size-lg;
  &--male { color: $status-info; }
  &--female { color: #FF69B4; }
}

.pf-tags {
  @include flex-align-center;
  gap: $spacing-sm;
  margin-top: $spacing-sm;
}

.pf-cert {
  height: 40rpx;
  padding: 0 16rpx;
  border-radius: 20rpx;
  @include flex-center;
  gap: 6rpx;
}

.pf-cert-icon {
  width: 24rpx;
  height: 24rpx;
  flex-shrink: 0;
}

.pf-cert--yes { background: #22C55E; }
.pf-cert--no { background: $status-offline; }

.pf-cert-text { font-size: $font-size-xs; color: $text-white; }

// ===== 游客态 =====
.pf-guest {
  position: relative;
  @include flex-column;
  align-items: center;
  padding: $spacing-xl 0 $spacing-lg;
}

.pf-guest-name {
  font-size: $font-size-xl;
  font-weight: $font-weight-bold;
  color: $text-white;
  margin-top: $spacing-sm;
}

.pf-login-btn {
  margin-top: $spacing-sm;
  height: 56rpx;
  padding: 0 $spacing-lg;
  border-radius: 28rpx;
  background: var(--tag-glass-bg, rgba(255, 255, 255, 0.25));
  @include flex-center;
}

.pf-login-text { font-size: $font-size-sm; color: $text-white; }

// ===== 底部占位 =====
.pf-spacer {
  height: calc(#{$tab-bar-height} + env(safe-area-inset-bottom) + #{$spacing-xl});
}
</style>
