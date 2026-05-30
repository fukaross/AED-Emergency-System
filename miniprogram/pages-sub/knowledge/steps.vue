<template>
  <view v-if="loading" class="steps-page" :class="themeClass">
    <view class="loading-placeholder">
      <text class="loading-text">加载中...</text>
    </view>
  </view>

  <view v-else-if="errorText" class="steps-page" :class="themeClass">
    <view class="error-placeholder">
      <text class="error-text">{{ errorText }}</text>
      <view class="retry-btn" @click="handleRetry">
        <text class="retry-btn-text">重试</text>
      </view>
    </view>
  </view>

  <view v-else-if="action" class="steps-page" :class="themeClass">
    <view v-if="!action.steps.length" class="empty-placeholder">
      <text class="empty-text">暂无步骤内容</text>
    </view>

    <view v-else class="steps-content">
      <view
        v-for="step in action.steps"
        :key="step.order"
        class="step-section"
      >
        <view class="step-header">
          <view class="step-header-num">{{ step.order }}</view>
          <text class="step-header-title">{{ step.title }}</text>
        </view>

        <image
          class="step-image"
          :src="step.image"
          mode="widthFix"
          @click="previewImage(step.image)"
        />

        <text class="step-desc">{{ step.description }}</text>

        <view v-if="step.keyPoints && step.keyPoints.length" class="step-points">
          <text
            v-for="point in step.keyPoints"
            :key="point"
            class="step-point"
          >⚠ {{ point }}</text>
        </view>
      </view>

      <view class="bottom-spacer" />
    </view>
  </view>
</template>

<script setup lang="ts">
import { onLoad } from '@dcloudio/uni-app'
import { useTheme } from '@/composables/useTheme'
import { useQuickAction } from '@/composables/useQuickAction'

const { themeClass } = useTheme()
const { action, loading, errorText, loadAction } = useQuickAction()

function previewImage(url: string) {
  uni.previewImage({ urls: [url], current: url })
}

function getActionId(): string {
  const pages = getCurrentPages()
  const current = pages[pages.length - 1]
  return (current as { options?: { actionId?: string } }).options?.actionId ?? ''
}

function handleRetry() {
  loadAction(getActionId())
}

onLoad(() => {
  loadAction(getActionId())
})
</script>

<style src="./steps.scss" lang="scss" scoped></style>
