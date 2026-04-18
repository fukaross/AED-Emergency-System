<template>
  <view v-if="visible" class="modal-wrapper">
    <view class="modal-overlay" @click="handleOverlayClick" />
    <view
      class="modal"
      :class="{ 'modal--visible': showContent }"
    >
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
 * 通用居中弹窗组件 (Modal)
 *
 * 规格：宽度 560rpx，圆角 24rpx，遮罩 rgba(0,0,0,0.5)
 * 动画：scale(0.8→1) + opacity(0→1)，300ms ease-out
 *
 * 详细设计见 CLAUDE.md §5.6 + GEMINI.md §3.3
 */
import { ref, watch, nextTick, useSlots } from 'vue'

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
}>()

const $slots = useSlots()

const visible = ref(false)
const showContent = ref(false)

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
.modal-wrapper {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 998;
  @include flex-center;
}

.modal-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.5);
}

.modal {
  position: relative;
  width: 560rpx;
  background-color: $background-white;
  border-radius: $radius-xl;
  z-index: 999;
  opacity: 0;
  transform: scale(0.8);
  transition: all $duration-slow $ease-out;
}

.modal--visible {
  opacity: 1;
  transform: scale(1);
}

.modal-header {
  @include flex-between;
  padding: $spacing-md $spacing-md $spacing-sm;
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
