<template>
  <view v-if="visible" class="modal-wrapper">
    <!-- 遮罩 -->
    <view
      class="modal-overlay"
      :class="{ 'modal-overlay--visible': showContent }"
      @click="handleOverlayClick"
    />

    <!-- 底部面板 -->
    <view
      class="modal"
      :class="{ 'modal--visible': showContent }"
    >
      <!-- 顶部拖拽指示条 -->
      <view class="modal-handle" />

      <view v-if="title" class="modal-header">
        <text class="modal-title">{{ title }}</text>
        <view v-if="showClose" class="modal-close" @click="close">
          <text class="modal-close-icon">✕</text>
        </view>
      </view>

      <view class="modal-body">
        <slot />
      </view>

      <view v-if="$slots.footer" class="modal-footer">
        <slot name="footer" />
      </view>
      <view v-else-if="showFooter" class="modal-footer modal-footer--default">
        <view class="modal-btn modal-btn--cancel" @click="close">
          <text>{{ cancelText }}</text>
        </view>
        <view class="modal-btn modal-btn--confirm" @click="confirm">
          <text>{{ confirmText }}</text>
        </view>
      </view>

    </view>
  </view>
</template>

<script setup lang="ts">
/**
 * 通用底部弹出面板 (Bottom Sheet)
 *
 * 动画采用单一 transition 方案：
 *   - 挂载后延迟一帧再进入可见态，确保小程序端能播出打开动画
 *   - 关闭时只移除可见态，不再叠加 closing class
 *
 * 这样在小程序端关闭时不会出现先抖一下、再收起的缩放错觉。
 */
import { ref, useSlots, watch } from 'vue'

interface Props {
  modelValue: boolean
  title?: string
  showClose?: boolean
  showFooter?: boolean
  cancelText?: string
  confirmText?: string
  closeOnOverlay?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  title: '',
  showClose: true,
  showFooter: true,
  cancelText: '关闭',
  confirmText: '确认',
  closeOnOverlay: true,
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirm: []
  'after-close': []
}>()

const $slots = useSlots()

const OPEN_DELAY_MS = 30

// 关闭时保留 DOM，等待 transition 完整播放后再卸载。
const CLOSE_DURATION_MS = 300

const visible = ref(false)
const showContent = ref(false)
let openTimer: ReturnType<typeof setTimeout> | null = null
let closeTimer: ReturnType<typeof setTimeout> | null = null

function clearOpenTimer() {
  if (!openTimer) {
    return
  }

  clearTimeout(openTimer)
  openTimer = null
}

function clearCloseTimer() {
  if (!closeTimer) {
    return
  }

  clearTimeout(closeTimer)
  closeTimer = null
}

function scheduleShow() {
  clearOpenTimer()
  openTimer = setTimeout(() => {
    openTimer = null
    if (props.modelValue) {
      showContent.value = true
    }
  }, OPEN_DELAY_MS)
}

function scheduleHide() {
  clearCloseTimer()
  closeTimer = setTimeout(() => {
    visible.value = false
    closeTimer = null
    emit('after-close')
  }, CLOSE_DURATION_MS)
}

watch(() => props.modelValue, (val) => {
  if (val) {
    clearOpenTimer()
    clearCloseTimer()
    showContent.value = false
    visible.value = true
    scheduleShow()
    return
  }

  clearOpenTimer()
  if (!visible.value) {
    return
  }

  showContent.value = false
  scheduleHide()
}, { immediate: true })

function close() {
  emit('update:modelValue', false)
}

function confirm() {
  emit('confirm')
}

function handleOverlayClick() {
  if (props.closeOnOverlay) {
    close()
  }
}
</script>

<style lang="scss" scoped>
// ===== 容器 =====
// bottom 与 TabBar 顶边对齐，并额外避开底部安全区。
// 遮罩和面板均在 TabBar 上方，TabBar 保持可见且不与弹窗重叠。
.modal-wrapper {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: calc(#{$tab-bar-height} + constant(safe-area-inset-bottom));
  bottom: calc(#{$tab-bar-height} + env(safe-area-inset-bottom));
  z-index: 1000;
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
}

// ===== 遮罩 =====
.modal-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.5);
  opacity: 0;
  transition: opacity $duration-normal $ease-default;
}

.modal-overlay--visible {
  opacity: 1;
}

// ===== 底部面板 =====
// 默认藏在屏幕下方，无动画
.modal {
  position: relative;
  width: 100%;
  max-height: 85vh;
  overflow-y: auto;
  background-color: $background-white;
  border-radius: $radius-xl $radius-xl 0 0;
  z-index: 1;
  transform: translateY(100%);
  transition: transform $duration-slow $ease-out;
}

.modal--visible {
  transform: translateY(0);
}

// ===== 内部结构 =====
.modal-handle {
  width: 80rpx;
  height: 8rpx;
  border-radius: 4rpx;
  background-color: $divider;
  margin: $spacing-sm auto $spacing-xs;
  flex-shrink: 0;
}

.modal-header {
  @include flex-between;
  padding: $spacing-sm $spacing-md;
}

.modal-title {
  font-size: $font-size-lg;
  font-weight: $font-weight-semibold;
  color: $text-primary;
}

.modal-close {
  width: 48rpx;
  height: 48rpx;
  @include flex-center;
}

.modal-close-icon {
  font-size: $font-size-base;
  color: $text-tertiary;
}

.modal-body {
  padding: $spacing-sm $spacing-md;
}

.modal-footer {
  padding: $spacing-sm $spacing-md $spacing-md;
}

.modal-footer--default {
  @include flex-between;
  gap: $spacing-sm;
}

.modal-btn {
  flex: 1;
  height: 80rpx;
  border-radius: 40rpx;
  @include flex-center;
}

.modal-btn--cancel {
  background-color: $background;
  color: $text-secondary;
  font-size: $font-size-lg;
}

.modal-btn--confirm {
  background-color: $primary;
  color: $text-white;
  font-size: $font-size-lg;
  font-weight: $font-weight-bold;

  &:active {
    background-color: $primary-dark;
  }
}


</style>
