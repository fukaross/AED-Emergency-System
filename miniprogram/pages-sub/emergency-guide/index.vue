<template>
  <view class="emergency-guide" :class="themeClass">
    <text>急救教程 — 页面骨架</text>
  </view>
</template>

<script setup lang="ts">
/**
 * 急救教程紧急入口页
 *
 * 来源：设备中心错误页面"急救教程"链接
 * 紧急场景设计：大号卡片、显眼操作按钮、减少阅读量
 * 入口：心肺复苏 / AED使用指南 / 海姆立克 / 一键拨打120
 *
 * 详细设计见 CLAUDE.md §4.5.6
 */
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
