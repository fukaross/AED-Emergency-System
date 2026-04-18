<template>
  <view class="archives">
    <text>急救档案 — 页面骨架</text>
  </view>
</template>

<script setup lang="ts">
/**
 * 急救档案列表页
 *
 * 搜索栏 + 级别筛选标签栏 + 档案卡片列表
 * 电话默认脱敏，完整号码需单独接口 + 确认弹窗 + 审计
 * 搜索/分页/筛选统一后端处理
 *
 * 详细设计见 CLAUDE.md §4.5.9
 */
</script>

<style lang="scss" scoped>
.archives {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
