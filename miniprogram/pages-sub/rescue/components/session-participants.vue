<template>
  <scroll-view class="participants-strip" scroll-x enable-flex>
    <view
      v-for="participant in participants"
      :key="participant.openId"
      class="participant-chip"
      :class="{ 'is-lost': participant.signal !== 'ok' }"
    >
      <view
        class="participant-dot"
        :style="{ backgroundColor: participantColor(participant.openId) }"
      />
      <text class="participant-name">{{ participant.displayName }}</text>
      <view class="participant-badges">
        <text v-if="participant.takenAed" class="badge badge--aed">拿了AED</text>
        <text v-if="participant.arrived" class="badge badge--arrived">已到达</text>
        <text v-if="participant.signal === 'lost'" class="badge badge--lost">信号丢失</text>
        <text v-if="participant.signal === 'gone'" class="badge badge--gone">已离线</text>
      </view>
    </view>
  </scroll-view>
</template>

<script setup lang="ts">
import type { RescueParticipant } from '@/types/rescue-session'
import type { ParticipantColor } from '../session-color'

const props = defineProps<{
  participants: readonly RescueParticipant[]
  colorMap: ReadonlyMap<string, ParticipantColor>
}>()

function participantColor(openId: string): string {
  return props.colorMap.get(openId)?.accent ?? '#9e9e9e'
}
</script>

<style lang="scss" scoped>
.participants-strip {
  display: flex;
  padding: $spacing-sm $spacing-md;
  background: $background-white;
  white-space: nowrap;
}

.participant-chip {
  display: inline-flex;
  align-items: center;
  gap: $spacing-xs;
  margin-right: $spacing-sm;
  padding: $spacing-xs $spacing-sm;
  background: $background;
  border-radius: $radius-lg;
}

.participant-dot {
  flex-shrink: 0;
  width: 16rpx;
  height: 16rpx;
  border-radius: $radius-full;
}

.participant-chip.is-lost {
  opacity: 0.55;
}

.participant-name {
  color: $text-primary;
  font-size: $font-size-sm;
  font-weight: $font-weight-bold;
}

.participant-badges {
  display: inline-flex;
  gap: $spacing-xs;
}

.badge {
  padding: 2rpx $spacing-xs;
  border-radius: $radius-sm;
  font-size: $font-size-xs;
}

.badge--aed {
  color: $text-white;
  background: $status-online;
}

.badge--arrived {
  color: $text-white;
  background: $primary;
}

.badge--lost,
.badge--gone {
  color: $text-secondary;
  background: $background-card-disabled;
}
</style>
