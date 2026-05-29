<template>
  <view class="step-guide" :class="themeClass">
    <text>急救步骤 — 页面骨架</text>
  </view>
</template>

<script setup lang="ts">
// 步骤指引（心肺复苏 / AED / 海姆立克）—— 大图 + 标题 + 上下步切换
import { useTheme } from '@/composables/useTheme'
const { themeClass } = useTheme()
</script>

<style lang="scss" scoped>
.step-guide {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
