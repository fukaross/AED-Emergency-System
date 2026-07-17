<template>
  <view class="rescue-view">
    <view class="view-header">
      <view class="live-dot" />
      <text class="view-title">实时救援 · 已有 {{ view.respondersCount }} 人响应</text>
    </view>

    <view v-if="view.participants.length" class="participant-row">
      <view
        v-for="(participant, index) in view.participants"
        :key="index"
        class="participant-chip"
        :class="{ 'is-lost': participant.signal !== 'ok' }"
      >
        <text class="participant-name">{{ participant.displayName }}</text>
        <text v-if="participant.takenAed" class="badge badge--aed">拿了AED</text>
        <text v-if="participant.arrived" class="badge badge--arrived">已到达</text>
        <text v-if="participant.signal === 'lost'" class="badge badge--lost">信号丢失</text>
      </view>
    </view>

    <view v-if="view.events.length" class="timeline">
      <view v-for="event in view.events" :key="event.seq" class="timeline-item">
        <view class="timeline-dot" :class="{ 'timeline-dot--revoke': event.action === 'revoke' }" />
        <text class="timeline-text">{{ formatRescueViewEventText(event) }}</text>
        <text class="timeline-time">{{ formatRelativeTime(event.createdAt) }}</text>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import type { RescueView } from '@/types/rescue-session'
import { formatRelativeTime } from '@/utils/format'
import { formatRescueViewEventText } from '@/utils/rescue-format'

defineProps<{
  view: RescueView
}>()
</script>

<style lang="scss" scoped>
.rescue-view {
  margin: $spacing-md;
  padding: $spacing-md;
  background: $background-white;
  border-radius: $radius-lg;
  box-shadow: $shadow-sm;
}

.view-header {
  display: flex;
  align-items: center;
  gap: $spacing-xs;
  margin-bottom: $spacing-sm;
}

.live-dot {
  width: 14rpx;
  height: 14rpx;
  border-radius: $radius-full;
  background: $primary;
  animation: pulse $duration-cycle infinite;
}

.view-title {
  color: $text-primary;
  font-size: $font-size-base;
  font-weight: $font-weight-semibold;
}

.participant-row {
  display: flex;
  flex-wrap: wrap;
  gap: $spacing-xs;
  margin-bottom: $spacing-sm;
}

.participant-chip {
  display: inline-flex;
  align-items: center;
  gap: $spacing-xs;
  padding: $spacing-xs $spacing-sm;
  background: $background;
  border-radius: $radius-lg;
}

.participant-chip.is-lost {
  opacity: 0.55;
}

.participant-name {
  color: $text-primary;
  font-size: $font-size-sm;
}

.badge {
  padding: 2rpx $spacing-xs;
  border-radius: $radius-sm;
  font-size: $font-size-xs;
  color: $text-white;
}

.badge--aed {
  background: $status-online;
}

.badge--arrived {
  background: $primary;
}

.badge--lost {
  color: $text-secondary;
  background: $background-card-disabled;
}

.timeline {
  border-top: 1rpx solid $divider-light;
  padding-top: $spacing-sm;
}

.timeline-item {
  display: flex;
  align-items: baseline;
  gap: $spacing-xs;
  margin-bottom: $spacing-xs;
}

.timeline-dot {
  flex-shrink: 0;
  width: 12rpx;
  height: 12rpx;
  border-radius: $radius-full;
  background: $status-online;
  transform: translateY(2rpx);
}

.timeline-dot--revoke {
  background: $status-offline;
}

.timeline-text {
  flex: 1;
  color: $text-primary;
  font-size: $font-size-sm;
}

.timeline-time {
  color: $text-tertiary;
  font-size: $font-size-xs;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.35; }
}
</style>
