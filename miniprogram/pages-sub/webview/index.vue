<template>
  <view class="webview-container">
    <web-view :src="url" />
  </view>
</template>

<script setup lang="ts">
/**
 * 外链技术容器页
 *
 * [G-03] 本页面为正式技术容器路由，必须保留在 pages.json 分包中。
 *        用于 Banner linkType="url" 等外链跳转场景。
 *        禁止删除或重命名此路由。
 *
 * 详细设计见 CLAUDE.md §6.2 + AGENTS.md 收敛结论
 */
import { ref } from 'vue'
import { onLoad } from '@dcloudio/uni-app'

const url = ref('')

onLoad((query) => {
  if (query?.url) {
    url.value = decodeURIComponent(query.url)
  }
})
</script>

<style lang="scss" scoped>
.webview-container {
  width: 100%;
  height: 100vh;
}
</style>
