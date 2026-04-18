<template>
  <view class="knowledge">
    <text>急救课堂 — 页面骨架</text>
    <custom-tab-bar />
  </view>
</template>

<script setup lang="ts">
/**
 * 急救课堂页面
 *
 * 轮播横幅 + 快速操作入口（CPR/海姆立克）+ 课程网格
 * 前两格固定：AED使用教程、心肺复苏教程
 *
 * 详细设计见 CLAUDE.md §4.3
 */
import CustomTabBar from '@/custom-tab-bar/index.vue'
</script>

<style lang="scss" scoped>
.knowledge {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
