<template>
  <view class="custom-navbar" :class="{ 'custom-navbar--transparent': transparent }">
    <view class="navbar-status-bar" :style="{ height: statusBarHeight + 'px' }" />
    <view class="navbar-content" :style="{ height: navBarHeight + 'px' }">
      <view v-if="showBack" class="navbar-back" @click="handleBack">
        <image
          v-if="backIcon"
          class="navbar-back-icon-img"
          :src="backIcon"
          mode="aspectFit"
        />
        <text v-else class="navbar-back-icon">‹</text>
      </view>
      <view class="navbar-title" :style="titleStyle">
        <text class="navbar-title-text">{{ title }}</text>
      </view>
      <view class="navbar-right">
        <slot name="right" />
      </view>
    </view>
  </view>
  <!-- 透明模式下不渲染占位，让内容（如地图）全屏铺开 -->
  <view v-if="!transparent" :style="{ height: totalHeight + 'px' }" />
</template>

<script setup lang="ts">
/**
 * 自定义沉浸式导航栏
 *
 * 使用页面：地图主页（透明渐变）、个人中心（融合头部）
 * 适配：优先使用 getWindowInfo + getMenuButtonBoundingClientRect
 * 右侧预留 ≥200rpx 避免与系统胶囊重叠
 *
 * 详细设计见 CLAUDE.md §4.1.2 + GEMINI.md §4.1
 */
import { ref, computed, onMounted } from 'vue'

interface Props {
  title?: string
  showBack?: boolean
  transparent?: boolean
  titleAlign?: 'left' | 'center'
  backIcon?: string
  beforeBack?: () => boolean | Promise<boolean>
}

const props = withDefaults(defineProps<Props>(), {
  title: '',
  showBack: false,
  transparent: false,
  titleAlign: 'left',
  backIcon: '',
  beforeBack: undefined,
})

const statusBarHeight = ref(0)
const navBarHeight = ref(44)
const menuButtonRight = ref(0)

const totalHeight = computed(() => statusBarHeight.value + navBarHeight.value)

const titleStyle = computed(() => ({
  textAlign: props.titleAlign,
  paddingRight: menuButtonRight.value + 'px',
}))

onMounted(() => {
  const windowInfo =
    typeof uni.getWindowInfo === 'function'
      ? uni.getWindowInfo()
      : uni.getSystemInfoSync()
  statusBarHeight.value = windowInfo.statusBarHeight ?? 0

  // #ifdef MP-WEIXIN
  try {
    const menuBtn = uni.getMenuButtonBoundingClientRect()
    navBarHeight.value =
      (menuBtn.top - statusBarHeight.value) * 2 + menuBtn.height
    menuButtonRight.value =
      windowInfo.windowWidth - menuBtn.left
  } catch {
    navBarHeight.value = 44
  }
  // #endif
})

async function handleBack() {
  if (props.beforeBack) {
    const canBack = await props.beforeBack()
    if (!canBack) return
  }
  uni.navigateBack({ delta: 1 })
}
</script>

<style lang="scss" scoped>
.custom-navbar {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 999;
  background-color: $background-white;
}

.custom-navbar--transparent {
  background: linear-gradient(
    to bottom,
    var(--bg-nav-transparent, rgba(255, 255, 255, 0.8)),
    rgba(255, 255, 255, 0)
  );
}

.navbar-status-bar {
  width: 100%;
}

.navbar-content {
  display: flex;
  align-items: center;
  padding: 0 $spacing-md;
}

.navbar-back {
  width: 60rpx;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: flex-start;
}

.navbar-back-icon {
  font-size: $font-size-hero;
  color: $text-primary;
  line-height: 1;
}

.navbar-back-icon-img {
  width: 40rpx;
  height: 40rpx;
  filter: var(--icon-arrow-filter, none);
}

.navbar-title {
  flex: 1;
  overflow: hidden;
}

.navbar-title-text {
  font-size: $font-size-xl;
  font-weight: $font-weight-semibold;
  color: $text-primary;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.navbar-right {
  flex-shrink: 0;
}
</style>
