<template>
  <view class="webview-container" :class="themeClass">
    <web-view v-if="url" :src="url" />
    <view v-else class="webview-error">
      <text class="webview-error__title">链接无效或缺失</text>
      <text class="webview-error__desc">请返回上一页重试，或检查外链参数是否完整。</text>
      <view class="webview-error__actions">
        <view class="webview-error__btn" @click="retryPageLoad">
          <text class="webview-error__btn-text">重试</text>
        </view>
        <view class="webview-error__btn webview-error__btn--ghost" @click="goBack">
          <text class="webview-error__btn-text webview-error__btn-text--ghost">返回</text>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
// 外链容器 —— Banner 跳转、外部链接都走这个路由，别删
import { ref } from 'vue'
import { onLoad } from '@dcloudio/uni-app'
import { useTheme } from '@/composables/useTheme'

const url = ref('')
const rawUrl = ref('')
const { themeClass } = useTheme()

function isValidWebUrl(target: string): boolean {
  return /^https:\/\//i.test(target)
}

function assignUrl(nextRawUrl?: string): void {
  rawUrl.value = nextRawUrl || ''
  if (!rawUrl.value) {
    url.value = ''
    return
  }

  const decodedUrl = decodeURIComponent(rawUrl.value)
  url.value = isValidWebUrl(decodedUrl) ? decodedUrl : ''
}

onLoad((query) => {
  assignUrl(query?.url)
})

function retryPageLoad(): void {
  assignUrl(rawUrl.value)
}

function goBack(): void {
  uni.navigateBack()
}
</script>

<style lang="scss" scoped>
.webview-container {
  width: 100%;
  height: 100vh;
}

.webview-error {
  min-height: 100vh;
  padding: $spacing-xl;
  background: $background;
  @include flex-column;
  justify-content: center;
  align-items: center;
  text-align: center;
}

.webview-error__title {
  font-size: $font-size-xl;
  font-weight: $font-weight-bold;
  color: $text-primary;
}

.webview-error__desc {
  margin-top: $spacing-sm;
  font-size: $font-size-base;
  color: $text-secondary;
  line-height: $line-height-relaxed;
}

.webview-error__actions {
  display: flex;
  gap: $spacing-sm;
  margin-top: $spacing-lg;
}

.webview-error__btn {
  @include btn-primary;
  min-width: 200rpx;
}

.webview-error__btn--ghost {
  background: $background-white;
  border: 2rpx solid $primary;
}

.webview-error__btn-text {
  color: $text-white;
  font-size: $font-size-base;
  font-weight: $font-weight-semibold;
}

.webview-error__btn-text--ghost {
  color: $primary;
}
</style>
