<template>
  <view class="step-guide" :class="themeClass">
    <text>急救步骤 — 页面骨架</text>
  </view>
</template>

<script setup lang="ts">
/**
 * 步骤指引页（心肺复苏 / AED使用 / 海姆立克急救法）
 *
 * 步骤计数 + 大图展示 + 标题 + 说明 + 上下步切换
 * 底部固定关键要点提示
 *
 * 详细设计见 CLAUDE.md §4.5.8
 */
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
