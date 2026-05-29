<template>
  <view class="course-detail" :class="themeClass">
    <text>课程详情 — 页面骨架</text>
  </view>
</template>

<script setup lang="ts">
// 课程详情（图文 / 视频模式）
import { useTheme } from '@/composables/useTheme'
const { themeClass } = useTheme()
</script>

<style lang="scss" scoped>
.course-detail {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
