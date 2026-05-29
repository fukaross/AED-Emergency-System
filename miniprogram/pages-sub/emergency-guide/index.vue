<template>
  <view class="emergency-guide" :class="themeClass">
    <text>急救教程 — 页面骨架</text>
  </view>
</template>

<script setup lang="ts">
// 急救教程紧急入口 —— 大卡片 + 显眼按钮，心肺复苏 / AED / 海姆立克 / 一键拨120
import { useTheme } from '@/composables/useTheme'
const { themeClass } = useTheme()
</script>

<style lang="scss" scoped>
.emergency-guide {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
