<template>
  <view class="device-center">
    <text>设备中心 — 页面骨架</text>
    <custom-tab-bar />
  </view>
</template>

<script setup lang="ts">
/**
 * 设备中心页面
 *
 * 正常态：AED 卡片列表（可用白底红标签 / 不可用灰底灰标签）
 * 错误态：NO_AED.svg 碎裂心形 + 排错建议 + 急救教程链接
 * 浮动按钮：右下角黄色问题报告按钮
 *
 * 详细设计见 CLAUDE.md §4.2
 */
import CustomTabBar from '@/custom-tab-bar/index.vue'
</script>

<style lang="scss" scoped>
.device-center {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
