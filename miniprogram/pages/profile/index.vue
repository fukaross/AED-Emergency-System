<template>
  <view class="profile">
    <text>个人中心 — 页面骨架</text>
    <custom-tab-bar />
  </view>
</template>

<script setup lang="ts">
/**
 * 个人中心页面
 *
 * 沉浸式头部（模糊背景 + 头像 + 用户名 + 认证/级别标签）
 * 选项列表：个人资料、急救档案、设置、我的学习、帮助反馈、关于
 * 游客态展示登录引导按钮
 *
 * 详细设计见 CLAUDE.md §4.4
 */
import CustomTabBar from '@/custom-tab-bar/index.vue'
</script>

<style lang="scss" scoped>
.profile {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
