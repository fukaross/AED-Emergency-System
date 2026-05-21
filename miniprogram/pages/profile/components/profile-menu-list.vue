<template>
  <view class="pf-menu">
    <view
      v-for="(item, idx) in PROFILE_MENU_ITEMS"
      :key="item.key"
      class="pf-menu-item"
      :class="{ 'pf-menu-item--last': idx === PROFILE_MENU_ITEMS.length - 1 }"
      @click="$emit('select', item)"
    >
      <image
        v-if="item.icon"
        class="pf-menu-icon"
        :src="item.icon"
        mode="aspectFit"
      />
      <view v-else class="pf-menu-icon pf-menu-icon-text">
        <text class="pf-menu-icon-text__glyph">{{ item.iconText }}</text>
      </view>
      <text class="pf-menu-title">{{ item.title }}</text>
      <text class="pf-menu-arrow">›</text>
    </view>
  </view>
</template>

<script setup lang="ts">
interface ProfileMenuItem {
  readonly key: string
  readonly title: string
  readonly icon?: string
  readonly iconText?: string
  readonly url: string
  readonly needAuth: boolean
}

const PROFILE_MENU_ITEMS: readonly ProfileMenuItem[] = [
  { key: 'profile', title: '个人资料', icon: '/static/icons/menu-profile.svg', url: '/pages-sub/profile/edit', needAuth: true },
  { key: 'volunteer', title: '加入志愿者', icon: '/static/icons/volunteer-prepare.svg', url: '/pages-sub/profile/volunteer', needAuth: true },
  { key: 'archive', title: '急救档案', icon: '/static/icons/menu-archive.svg', url: '/pages-sub/profile/archives', needAuth: true },
  { key: 'help-history', title: '求助历史', iconText: '☎', url: '/pages-sub/help/history', needAuth: true },
  { key: 'settings', title: '设置', icon: '/static/icons/menu-settings.svg', url: '/pages-sub/profile/settings', needAuth: true },
  { key: 'study', title: '我的学习', icon: '/static/icons/menu-study.svg', url: '', needAuth: true },
  { key: 'feedback', title: '帮助与反馈', icon: '/static/icons/menu-feedback.svg', url: '', needAuth: false },
  { key: 'about', title: '关于我们', icon: '/static/icons/menu-about.svg', url: '', needAuth: false },
]

defineEmits<{
  (event: 'select', item: ProfileMenuItem): void
}>()
</script>

<style lang="scss" scoped>
.pf-menu {
  background: $background-white;
  margin: -16rpx $spacing-md 0;
  border-radius: $radius-xl $radius-xl 0 0;
  position: relative;
  z-index: 1;
}

.pf-menu-item {
  @include flex-align-center;
  height: 100rpx;
  padding: 0 $spacing-md;
  border-bottom: 1rpx solid $divider-light;
  margin-left: 88rpx;
  position: relative;
}

.pf-menu-item--last {
  border-bottom: none;
}

.pf-menu-icon {
  position: absolute;
  left: -64rpx;
  width: 48rpx;
  height: 48rpx;
}

.pf-menu-icon-text {
  @include flex-center;
}

.pf-menu-icon-text__glyph {
  color: $text-tertiary;
  font-size: $font-size-xl;
  line-height: 48rpx;
}

.pf-menu-title {
  flex: 1;
  font-size: $font-size-lg;
  color: $text-primary;
}

.pf-menu-arrow {
  font-size: $font-size-xl;
  color: $text-tertiary;
}
</style>
