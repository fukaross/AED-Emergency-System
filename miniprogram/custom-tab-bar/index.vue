<template>
  <view class="tab-bar">
    <view
      v-for="(tab, index) in tabs"
      :key="tab.pagePath"
      class="tab-item"
      :class="{ 'tab-item--active': currentIndex === index }"
      @click="switchTab(index)"
    >
      <view class="tab-icon-wrapper">
        <image
          class="tab-icon"
          :src="currentIndex === index ? tab.activeIcon : tab.icon"
          mode="aspectFit"
        />
      </view>
      <text class="tab-label">{{ tab.text }}</text>
    </view>
  </view>
</template>

<script setup lang="ts">
/**
 * 自定义 TabBar 组件
 *
 * 4 Tab：首页、设备、课堂、个人
 * 激活色使用主色令牌，未激活色使用次级文字令牌
 * 图标 48rpx，文字 20rpx，高度 100rpx + safe-area
 * 选中动画 scale(0.9→1.05→1.0) 200ms ease
 *
 * 图标资源从 static/icons/ 读取
 *
 * 详细设计见 CLAUDE.md §5.5 + GEMINI.md §4.2
 */
import { ref, onMounted } from 'vue'

interface TabItem {
  readonly pagePath: string
  readonly text: string
  readonly icon: string
  readonly activeIcon: string
}

const TAB_PATHS = [
  '/pages/map-home/index',
  '/pages/device-center/index',
  '/pages/knowledge/index',
  '/pages/profile/index',
] as const

const tabs: readonly TabItem[] = [
  {
    pagePath: TAB_PATHS[0],
    text: '首页',
    icon: '/static/icons/tab-home.svg',
    activeIcon: '/static/icons/tab-home-active.svg',
  },
  {
    pagePath: TAB_PATHS[1],
    text: '设备',
    icon: '/static/icons/tab-device.svg',
    activeIcon: '/static/icons/tab-device-active.svg',
  },
  {
    pagePath: TAB_PATHS[2],
    text: '课堂',
    icon: '/static/icons/tab-course.svg',
    activeIcon: '/static/icons/tab-course-active.svg',
  },
  {
    pagePath: TAB_PATHS[3],
    text: '个人',
    icon: '/static/icons/tab-profile.svg',
    activeIcon: '/static/icons/tab-profile-active.svg',
  },
]

const currentIndex = ref(0)

onMounted(() => {
  const pages = getCurrentPages()
  if (pages.length > 0) {
    const currentRoute = '/' + pages[pages.length - 1].route
    const matchedIndex = TAB_PATHS.indexOf(currentRoute as typeof TAB_PATHS[number])
    if (matchedIndex !== -1) {
      currentIndex.value = matchedIndex
    }
  }
})

function switchTab(index: number) {
  if (index === currentIndex.value) return
  currentIndex.value = index
  uni.switchTab({ url: tabs[index].pagePath })
}
</script>

<style lang="scss" scoped>
$tab-active-color: $primary;
$tab-inactive-color: $text-tertiary;
$tab-bg: $background-white;
$tab-border: $divider;
$tab-height: 100rpx;
$tab-icon-size: 48rpx;
$tab-font-size: 20rpx;

.tab-bar {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 999;
  display: flex;
  align-items: center;
  height: $tab-height;
  box-sizing: content-box;
  padding-bottom: constant(safe-area-inset-bottom);
  padding-bottom: env(safe-area-inset-bottom);
  background-color: $tab-bg;
  border-top: 1rpx solid $tab-border;
}

.tab-item {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  position: relative;
}

.tab-icon-wrapper {
  width: $tab-icon-size;
  height: $tab-icon-size;
  display: flex;
  align-items: center;
  justify-content: center;
}

.tab-icon {
  width: $tab-icon-size;
  height: $tab-icon-size;
}

.tab-label {
  font-size: $tab-font-size;
  line-height: 1.2;
  margin-top: 4rpx;
  color: $tab-inactive-color;
}

.tab-item--active {
  .tab-label {
    color: $tab-active-color;
  }

  .tab-icon-wrapper {
    animation: tab-bounce 200ms ease;
  }
}

@keyframes tab-bounce {
  0% { transform: scale(0.9); }
  50% { transform: scale(1.05); }
  100% { transform: scale(1); }
}
</style>
