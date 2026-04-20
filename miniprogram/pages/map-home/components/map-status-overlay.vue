<template>
  <view class="map-status-overlay">
    <view v-if="statusMessage && !statusBarDismissed" class="status-bar">
      <text class="status-bar__text">{{ statusMessage }}</text>
      <view class="status-bar__close" @click="handleDismissStatus">
        <text class="status-bar__close-icon">✕</text>
      </view>
    </view>

    <view v-if="showLoading" class="loading-tip">
      <text class="loading-tip__text">定位与设备加载中...</text>
    </view>
  </view>
</template>

<script setup lang="ts">
interface Props {
  readonly statusMessage?: string
  readonly statusBarDismissed?: boolean
  readonly showLoading?: boolean
}

withDefaults(defineProps<Props>(), {
  statusMessage: '',
  statusBarDismissed: false,
  showLoading: false,
})

const emit = defineEmits<{
  'dismiss-status': []
}>()

function handleDismissStatus(): void {
  emit('dismiss-status')
}
</script>

<style lang="scss" scoped>
.status-bar {
  position: fixed;
  right: $spacing-md;
  bottom: calc(#{$tab-bar-height} + #{$spacing-md} + env(safe-area-inset-bottom) + 120rpx + #{$spacing-sm} + 100rpx + #{$spacing-sm});
  max-width: 480rpx;
  padding: $spacing-sm $spacing-md;
  border-radius: $radius-md;
  background-color: $background-white;
  box-shadow: $shadow-sm;
  z-index: 99;
  display: flex;
  align-items: center;
  gap: $spacing-sm;

  &__text {
    flex: 1;
    font-size: $font-size-sm;
    color: $status-error;
  }

  &__close {
    flex-shrink: 0;
    width: 40rpx;
    height: 40rpx;
    @include flex-center;
  }

  &__close-icon {
    font-size: $font-size-sm;
    color: $text-tertiary;
  }
}

.loading-tip {
  position: fixed;
  top: calc(var(--navbar-bottom, 180rpx) + 110rpx);
  left: 50%;
  transform: translateX(-50%);
  padding: $spacing-xs $spacing-md;
  border-radius: $radius-pill;
  background-color: $background-white;
  box-shadow: $shadow-sm;
  z-index: 99;

  &__text {
    font-size: $font-size-sm;
    color: $text-secondary;
  }
}
</style>
