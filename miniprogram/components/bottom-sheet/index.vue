<template>
  <view
    v-if="visible"
    class="bottom-sheet-wrapper"
    :class="{ 'bottom-sheet-wrapper--respect-tab-bar': respectTabBar }"
  >
    <view class="bottom-sheet-overlay" @click="handleOverlayClick" />
    <view
      class="bottom-sheet"
      :class="{ 'bottom-sheet--visible': showContent }"
      :style="sheetStyle"
    >
      <view class="bottom-sheet-handle">
        <view class="bottom-sheet-handle-bar" />
      </view>
      <view v-if="title" class="bottom-sheet-header">
        <text class="bottom-sheet-title">{{ title }}</text>
        <view class="bottom-sheet-close" @click="close">
          <text class="bottom-sheet-close-icon">✕</text>
        </view>
      </view>
      <scroll-view
        class="bottom-sheet-body"
        :class="{ 'bottom-sheet-body--respect-tab-bar': respectTabBar }"
        :style="bodyStyle"
        scroll-y
      >
        <slot />
      </scroll-view>
    </view>
  </view>
</template>

<script setup lang="ts">
/**
 * 通用底部弹窗组件 (Bottom Sheet)
 *
 * [G-02] 附近 AED 面板使用本组件，交互为点击展开/收起，
 *        不实现拖拽。顶部横条仅为视觉提示。
 *
 * 规格：最大高度 70vh，顶部圆角 24rpx，动画 300ms ease-out
 *
 * 详细设计见 CLAUDE.md §5.6 + GEMINI.md §3.3
 */
import { computed, ref, watch, nextTick } from 'vue'

interface Props {
  modelValue: boolean
  title?: string
  maxHeight?: string
  closeOnOverlay?: boolean
  respectTabBar?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  title: '',
  maxHeight: '70vh',
  closeOnOverlay: true,
  respectTabBar: false,
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>()

const visible = ref(false)
const showContent = ref(false)

const SHEET_HANDLE_HEIGHT = '28rpx'
const SHEET_HEADER_HEIGHT = '64rpx'

const sheetStyle = computed(() => ({
  height: props.maxHeight,
  maxHeight: props.maxHeight,
}))

const bodyStyle = computed(() => ({
  height: `calc(${props.maxHeight} - ${SHEET_HANDLE_HEIGHT} - ${props.title ? SHEET_HEADER_HEIGHT : '0rpx'})`,
}))

watch(() => props.modelValue, (val) => {
  if (val) {
    visible.value = true
    nextTick(() => { showContent.value = true })
  } else {
    showContent.value = false
    setTimeout(() => { visible.value = false }, 300)
  }
}, { immediate: true })

function close() {
  emit('update:modelValue', false)
}

function handleOverlayClick() {
  if (props.closeOnOverlay) {
    close()
  }
}
</script>

<style lang="scss" scoped>
.bottom-sheet-wrapper {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 1200;
}

.bottom-sheet-wrapper--respect-tab-bar {
  bottom: calc(#{$tab-bar-height} + constant(safe-area-inset-bottom));
  bottom: calc(#{$tab-bar-height} + env(safe-area-inset-bottom));
}

.bottom-sheet-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.5);
}

.bottom-sheet {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background-color: $background-white;
  border-radius: $radius-xl $radius-xl 0 0;
  transform: translateY(100%);
  transition: transform $duration-slow $ease-out;
  z-index: 1201;
}

.bottom-sheet--visible {
  transform: translateY(0);
}

.bottom-sheet-handle {
  @include flex-center;
  padding: 12rpx 0 8rpx;
}

.bottom-sheet-handle-bar {
  width: 40rpx;
  height: 8rpx;
  background-color: $divider;
  border-radius: 4rpx;
}

.bottom-sheet-header {
  @include flex-between;
  padding: 0 $spacing-md $spacing-sm;
}

.bottom-sheet-title {
  font-size: $font-size-lg;
  font-weight: $font-weight-semibold;
  color: $text-primary;
}

.bottom-sheet-close {
  width: 48rpx;
  height: 48rpx;
  @include flex-center;
}

.bottom-sheet-close-icon {
  font-size: $font-size-base;
  color: $text-tertiary;
}

.bottom-sheet-body {
  flex: 1;
  min-height: 0;
  box-sizing: border-box;
  padding: 0 $spacing-md calc(#{$tab-bar-height} + env(safe-area-inset-bottom) + #{$spacing-lg});
}

.bottom-sheet-body--respect-tab-bar {
  padding-bottom: $spacing-lg;
}
</style>
