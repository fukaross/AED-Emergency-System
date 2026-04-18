<template>
  <view class="settings">
    <text>设置 — 页面骨架</text>
  </view>
</template>

<script setup lang="ts">
/**
 * 设置页
 *
 * 选项：消息通知、位置权限、清除缓存、深色模式、大字模式、减弱动画、退出登录
 * 全局展示设置持久化到 settings store + Storage
 *
 * 详细设计见 CLAUDE.md §4.5.11
 */
</script>

<style lang="scss" scoped>
.settings {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
