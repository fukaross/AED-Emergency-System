<template>
  <view v-if="items.length" class="session-card" :style="cardStyle">
    <scroll-view class="session-list" :style="sessionListStyle" scroll-y>
      <view
        v-for="item in items"
        :key="item.helpId"
        class="session-row"
        @tap="emit('open', item.helpId)"
      >
        <view class="pulse-dot" :class="{ 'is-unjoined': !item.joined }" />
        <view class="card-main">
          <view class="title-row">
            <text class="card-title">急救会话</text>
            <text v-if="!item.joined" class="join-state">待响应</text>
          </view>
          <text class="card-sub">{{ item.address }}</text>
        </view>
        <view v-if="getSessionUnreadText(item)" class="unread-badge">
          <text class="unread-text">{{ getSessionUnreadText(item) }}</text>
        </view>
        <text class="enter-text">进入 ›</text>
      </view>
    </scroll-view>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useNavbarSafeArea } from '@/composables/useNavbarSafeArea'
import { getSessionUnreadText } from '@/pages/map-home/rescue-session-entry'
import type { RescueMineItem } from '@/types/rescue-session'

const props = defineProps<{
  readonly items: readonly RescueMineItem[]
  /** 主页已有求助卡片时向下错位，避免遮挡 */
  readonly stacked: boolean
}>()

const emit = defineEmits<{
  (event: 'open', helpId: string): void
}>()

const { navbarBottom } = useNavbarSafeArea()

const STACKED_OFFSET_PX = 56
const SESSION_ROW_HEIGHT_RPX = 112
const MAX_VISIBLE_ROWS = 3

const cardStyle = computed(() => ({
  top: `${navbarBottom.value + 12 + (props.stacked ? STACKED_OFFSET_PX : 0)}px`,
}))

const sessionListStyle = computed(() => ({
  height: `${Math.min(props.items.length, MAX_VISIBLE_ROWS) * SESSION_ROW_HEIGHT_RPX}rpx`,
}))

</script>

<style lang="scss" scoped>
.session-card {
  position: fixed;
  left: $spacing-md;
  z-index: 101;
  display: flex;
  width: calc(100vw - 64rpx);
  max-width: 540rpx;
  max-height: 336rpx;
  overflow: hidden;
  border-radius: $radius-lg;
  background: $background-white;
  box-shadow: $shadow-md;
}

.session-list {
  width: 100%;
  max-height: 336rpx;
}

.session-row {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  box-sizing: border-box;
  height: 112rpx;
  padding: $spacing-sm;
}

.session-row + .session-row {
  border-top: 1rpx solid $divider-light;
}

.pulse-dot {
  flex-shrink: 0;
  width: 16rpx;
  height: 16rpx;
  border-radius: $radius-full;
  background: $primary;
  animation: pulse $duration-cycle infinite;
}

.pulse-dot.is-unjoined {
  background: $status-warning;
}

.card-main {
  flex: 1;
  min-width: 0;
}

.title-row {
  display: flex;
  align-items: center;
  gap: $spacing-xs;
}

.card-title {
  display: block;
  color: $text-primary;
  font-size: $font-size-sm;
  font-weight: $font-weight-semibold;
}

.join-state {
  color: $status-warning;
  font-size: $font-size-xs;
}

.card-sub {
  @include text-ellipsis;
  display: block;
  max-width: 360rpx;
  margin-top: 4rpx;
  color: $text-secondary;
  font-size: $font-size-xs;
}

.unread-badge {
  flex-shrink: 0;
  min-width: 36rpx;
  height: 36rpx;
  padding: 0 $spacing-xs;
  border-radius: $radius-pill;
  background: $primary;
  display: flex;
  align-items: center;
  justify-content: center;
}

.unread-text {
  color: $text-white;
  font-size: $font-size-xs;
  font-weight: $font-weight-bold;
}

.enter-text {
  flex-shrink: 0;
  color: $primary;
  font-size: $font-size-xs;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.35; }
}
</style>
