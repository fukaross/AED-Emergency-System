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
    <template v-else>
    <scroll-view scroll-y class="steps-scroll" :enhanced="true" :show-scrollbar="false">
      <view class="step-main" @touchstart="onTouchStart" @touchend="onTouchEnd">
        <text class="step-counter">{{ currentIndex + 1 }} / {{ action.steps.length }}</text>

        <view :key="currentIndex" class="step-card" :class="transitionClass">
          <image
            class="step-image"
            :src="currentStep.image"
            mode="aspectFill"
            @click="previewImage(currentStep.image)"
          />
        </view>

        <text class="step-title">{{ currentStep.title }}</text>
        <text class="step-desc">{{ currentStep.description }}</text>
      </view>
    </scroll-view>

    <view class="step-controls">
      <view
        class="nav-btn prev"
        :class="{ hidden: currentIndex === 0 }"
        @click="goPrev"
      >
        <text class="nav-btn-text">← 上一步</text>
      </view>
      <view v-if="action.steps.length > 2" class="dot-indicators">
        <view
          v-for="(_, idx) in action.steps"
          :key="idx"
          class="dot"
          :class="{ active: idx === currentIndex }"
        />
      </view>
      <view
        class="nav-btn next"
        :class="{ done: isLastStep }"
        @click="goNext"
      >
        <text class="nav-btn-text">{{ isLastStep ? '完成' : '下一步 →' }}</text>
      </view>
    </view>

      <view
        v-if="currentStep.keyPoints && currentStep.keyPoints.length"
        class="key-points-bar"
      >
      <text
        v-for="point in currentStep.keyPoints"
        :key="point"
        class="key-point"
      >⚠ {{ point }}</text>
    </view>
  </template>
  </view>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { onLoad } from '@dcloudio/uni-app'
import { useTheme } from '@/composables/useTheme'
import { useSwipe } from '@/composables/useSwipe'
import { useQuickAction } from '@/composables/useQuickAction'
import type { QuickActionStep } from '@/types/knowledge'

const { themeClass } = useTheme()
const { action, loading, errorText, loadAction } = useQuickAction()
const currentIndex = ref(0)

const currentStep = computed<QuickActionStep>(() => {
  return action.value?.steps?.[currentIndex.value] ?? ({} as QuickActionStep)
})
const isFirstStep = computed(() => currentIndex.value === 0)
const isLastStep = computed(() => {
  const steps = action.value?.steps
  return steps ? currentIndex.value >= steps.length - 1 : true
})

const { transitionClass, onTouchStart, onTouchEnd } = useSwipe(
  () => { if (!isLastStep.value) currentIndex.value++ },
  () => { if (!isFirstStep.value) currentIndex.value-- },
)

function goPrev() {
  if (isFirstStep.value) return
  transitionClass.value = 'slide-in-left'
  currentIndex.value--
}

function goNext() {
  if (isLastStep.value) {
    uni.navigateBack()
    return
  }
  transitionClass.value = 'slide-in-right'
  currentIndex.value++
}

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
