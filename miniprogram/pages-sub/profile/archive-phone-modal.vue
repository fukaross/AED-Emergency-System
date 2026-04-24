<template>
  <view v-if="visible" class="ar-overlay" @tap="emit('close')">
    <view class="ar-modal" @tap.stop>
      <view class="ar-modal-header">
        <text class="ar-modal-title">查看完整联系方式</text>
      </view>
      <view class="ar-modal-body">
        <text class="ar-modal-desc">
          确认查看{{ archiveName }}的完整联系方式？
        </text>
        <text class="ar-modal-warn">仅用于紧急联络，请勿滥用。</text>
        <input
          v-model="purposeValue"
          class="ar-modal-input"
          placeholder="请填写查看用途（必填）"
          placeholder-class="ar-modal-placeholder"
        />
      </view>
      <view class="ar-modal-actions">
        <view class="ar-modal-btn ar-modal-btn--cancel" @tap="emit('close')">
          <text>取消</text>
        </view>
        <view
          class="ar-modal-btn ar-modal-btn--confirm"
          :class="{ 'ar-modal-btn--disabled': confirmDisabled }"
          @tap="emit('confirm')"
        >
          <text>{{ phoneLoading ? '获取中...' : '确认查看' }}</text>
        </view>
      </view>
      <view v-if="fullPhone" class="ar-modal-phone">
        <text class="ar-modal-phone-num">{{ fullPhone }}</text>
        <view
          class="ar-modal-phone-call"
          :class="{ 'ar-modal-phone-call--loading': phoneLoading }"
          @tap="emit('call')"
        >
          <text>拨号</text>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  readonly visible: boolean
  readonly archiveName: string
  readonly purpose: string
  readonly fullPhone: string | null
  readonly phoneLoading: boolean
}

const props = defineProps<Props>()

const emit = defineEmits<{
  close: []
  confirm: []
  call: []
  'update:purpose': [value: string]
}>()

const confirmDisabled = computed(
  () => !props.purpose.trim() || props.phoneLoading,
)

const purposeValue = computed({
  get: () => props.purpose,
  set: (value: string) => emit('update:purpose', value),
})
</script>

<style lang="scss" scoped>
.ar-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 1000;
  background: $overlay-mask;
  display: flex;
  align-items: center;
  justify-content: center;
}

.ar-modal {
  width: 560rpx;
  background: $background-white;
  border-radius: $radius-xl;
  overflow: hidden;
}

.ar-modal-header {
  padding: $spacing-lg $spacing-md $spacing-sm;
  text-align: center;
}

.ar-modal-title {
  font-size: $font-size-xl;
  font-weight: $font-weight-semibold;
  color: $text-primary;
}

.ar-modal-body {
  padding: 0 $spacing-md $spacing-md;
}

.ar-modal-desc {
  display: block;
  margin-bottom: $spacing-xs;
  font-size: $font-size-base;
  color: $text-primary;
  text-align: center;
}

.ar-modal-warn {
  display: block;
  margin-bottom: $spacing-md;
  font-size: $font-size-sm;
  color: $status-warning;
  text-align: center;
}

.ar-modal-input {
  height: 72rpx;
  padding: 0 $spacing-sm;
  border: 2rpx solid $divider;
  border-radius: $radius-md;
  font-size: $font-size-base;
  color: $text-primary;
}

.ar-modal-placeholder {
  color: $text-tertiary;
  font-size: $font-size-base;
}

.ar-modal-actions {
  display: flex;
  border-top: 2rpx solid $divider;
}

.ar-modal-btn {
  flex: 1;
  height: 88rpx;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: $font-size-lg;
}

.ar-modal-btn--cancel {
  color: $text-secondary;
  border-right: 2rpx solid $divider;
}

.ar-modal-btn--confirm {
  color: $primary;
  font-weight: $font-weight-semibold;
}

.ar-modal-btn--disabled {
  color: $text-disabled;
}

.ar-modal-phone {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: $spacing-md;
  padding: $spacing-md;
  border-top: 2rpx solid $divider;
}

.ar-modal-phone-num {
  font-size: $font-size-xl;
  font-weight: $font-weight-semibold;
  color: $text-primary;
  letter-spacing: 2rpx;
}

.ar-modal-phone-call {
  height: 56rpx;
  padding: 0 $spacing-lg;
  border-radius: 28rpx;
  background: $primary;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: $font-size-base;
  color: $text-white;
  font-weight: $font-weight-medium;
}

.ar-modal-phone-call--loading {
  opacity: 0.6;
}
</style>
