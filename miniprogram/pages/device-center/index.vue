<template>
  <view class="device-center-deprecated">
    <text class="dep-text">设备中心已迁移至急救员中心</text>
  </view>
</template>

<script setup lang="ts">
// 历史文件，已废弃 —— 第二个 Tab 已迁移至 pages/rescuer-center/index
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
