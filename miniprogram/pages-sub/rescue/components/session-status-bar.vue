<template>
  <view class="status-bar">
    <view
      class="status-btn"
      :class="{ 'is-active': me?.takenAed, 'is-disabled': disabled || pending }"
      @click="handleToggle('taken_aed')"
    >
      <text class="status-btn-text">{{ statusLabel('taken_aed') }}</text>
    </view>
    <view
      class="status-btn status-btn--secondary"
      :class="{ 'is-active': me?.arrived, 'is-disabled': disabled || pending }"
      @click="handleToggle('arrived')"
    >
      <text class="status-btn-text">{{ statusLabel('arrived') }}</text>
    </view>
  </view>
</template>

<script setup lang="ts">
import type { RescueMe, RescueMutableEventType } from '@/types/rescue-session'

const props = defineProps<{
  me: RescueMe | null
  disabled: boolean
  pending: boolean
}>()

const emit = defineEmits<{
  toggle: [eventType: RescueMutableEventType]
}>()

function handleToggle(eventType: RescueMutableEventType): void {
  if (props.disabled || props.pending) return
  emit('toggle', eventType)
}

function statusLabel(eventType: RescueMutableEventType): string {
  if (props.pending) return '同步中'
  if (eventType === 'taken_aed') return props.me?.takenAed ? '✓ 已拿取 AED' : '我已拿取 AED'
  return props.me?.arrived ? '✓ 已到达现场' : '我已到达现场'
}
</script>

<style lang="scss" scoped>
.status-bar {
  display: flex;
  gap: $spacing-md;
  padding: $spacing-md $spacing-md $spacing-sm;
  background: $background-white;
}

.status-btn {
  @include btn-primary;
  flex: 1;
}

.status-btn--secondary {
  @include btn-secondary;
}

.status-btn.is-active {
  background-color: $status-online;
  box-shadow: none;
}

.status-btn--secondary.is-active {
  background-color: $status-online;
  color: $text-white;
  box-shadow: none;
}

.status-btn.is-disabled {
  background-color: $background-card-disabled;
  box-shadow: none;
  pointer-events: none;
}

.status-btn-text {
  color: $text-white;
  font-size: $font-size-base;
  font-weight: $font-weight-bold;
}

.status-btn--secondary .status-btn-text {
  color: $primary;
}

.status-btn--secondary.is-active .status-btn-text,
.status-btn--secondary.is-disabled .status-btn-text {
  color: $text-white;
}
</style>
