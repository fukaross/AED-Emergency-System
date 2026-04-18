<template>
  <view class="tab-bar">
    <view
      v-for="(tab, idx) in TABS"
      :key="tab.pagePath"
      class="tab-item"
      :class="{ 'tab-item--active': selectedIndex === idx }"
      @click="switchTab(idx)"
    >
      <view class="tab-icon-wrapper">
        <image
          class="tab-icon"
          :src="selectedIndex === idx ? tab.activeIcon : tab.icon"
          mode="aspectFit"
        />
      </view>
      <text class="tab-label">{{ tab.text }}</text>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface TabItem {
  readonly pagePath: string
  readonly text: string
  readonly icon: string
  readonly activeIcon: string
}

interface Props {
  readonly currentPath: string
}

const props = defineProps<Props>()

const TABS: readonly TabItem[] = [
  {
    pagePath: '/pages/map-home/index',
    text: '首页',
    icon: '/static/icons/tab-home.svg',
    activeIcon: '/static/icons/tab-home-active.svg',
  },
  {
    pagePath: '/pages/device-center/index',
    text: '设备',
    icon: '/static/icons/tab-device.svg',
    activeIcon: '/static/icons/tab-device-active.svg',
  },
  {
    pagePath: '/pages/knowledge/index',
    text: '课堂',
    icon: '/static/icons/tab-course.svg',
    activeIcon: '/static/icons/tab-course-active.svg',
  },
  {
    pagePath: '/pages/profile/index',
    text: '个人',
    icon: '/static/icons/tab-profile.svg',
    activeIcon: '/static/icons/tab-profile-active.svg',
  },
]

const selectedIndex = computed(() => {
  const index = TABS.findIndex((tab) => tab.pagePath === props.currentPath)
  return index >= 0 ? index : 0
})

function switchTab(index: number): void {
  if (index === selectedIndex.value) {
    return
  }

  uni.switchTab({ url: TABS[index].pagePath })
}
</script>

<style lang="scss" scoped>
.tab-bar {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 999;
  display: flex;
  align-items: center;
  height: $tab-bar-height;
  box-sizing: content-box;
  padding-bottom: constant(safe-area-inset-bottom);
  padding-bottom: env(safe-area-inset-bottom);
  background-color: $background-white;
  border-top: 1rpx solid $divider;
}

.tab-item {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.tab-icon-wrapper {
  width: 48rpx;
  height: 48rpx;
  display: flex;
  align-items: center;
  justify-content: center;
}

.tab-icon {
  width: 48rpx;
  height: 48rpx;
}

.tab-label {
  margin-top: 4rpx;
  font-size: $font-size-xs;
  line-height: $line-height-tight;
  color: $text-tertiary;
}

.tab-item--active {
  .tab-label {
    color: $primary;
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
