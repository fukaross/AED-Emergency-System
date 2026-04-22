<template>
  <view class="device-center-deprecated">
    <text class="dep-text">设备中心已迁移至急救员中心</text>
  </view>
</template>

<script setup lang="ts">
/**
 * ⚠️ 历史文件，已废弃
 *
 * 第二个 Tab 的正式运行时入口已迁移至 pages/rescuer-center/index。
 * 本文件仅保留作为历史参考，不再参与运行时路由。
 */
</script>

<style lang="scss" scoped>
.device-center-deprecated {
  min-height: 100vh;
  @include flex-center;
  background: $background;
}

.dep-text {
  font-size: $font-size-lg;
  color: $text-tertiary;
}
</style>
