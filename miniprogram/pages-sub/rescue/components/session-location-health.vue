<template>
  <view
    v-if="health.status !== 'idle' && health.status !== 'healthy'"
    class="location-health"
  >
    <text class="location-health__text">{{ health.message }}</text>
    <button
      v-if="health.status === 'permission_denied'"
      class="location-health__action"
      @click="$emit('openSettings')"
    >
      去设置
    </button>
  </view>
</template>

<script setup lang="ts">
import type { SessionLocationHealth } from '@/composables/useSessionLocationReporter'

defineProps<{ health: SessionLocationHealth }>()
defineEmits<{ openSettings: [] }>()
</script>

<style lang="scss" scoped>
.location-health {
  position: absolute;
  right: $spacing-md;
  bottom: $spacing-sm;
  left: $spacing-md;
  z-index: 2;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: $spacing-sm;
  min-height: 64rpx;
  padding: $spacing-xs $spacing-sm;
  background: rgba(31, 41, 55, 0.88);
  border-radius: $radius-md;
}

.location-health__text {
  color: $text-white;
  font-size: $font-size-sm;
}

.location-health__action {
  flex-shrink: 0;
  margin: 0;
  padding: 0 $spacing-sm;
  color: $text-white;
  font-size: $font-size-sm;
  line-height: 48rpx;
  background: transparent;
  border: 1rpx solid rgba(255, 255, 255, 0.7);
  border-radius: $radius-sm;
}

.location-health__action::after {
  border: 0;
}
</style>
