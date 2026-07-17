<template>
  <view v-if="item" class="session-card" :style="cardStyle" @click="emit('open')">
    <view class="pulse-dot" />
    <view class="card-main">
      <text class="card-title">急救会话进行中</text>
      <text class="card-sub">{{ item.address }}</text>
    </view>
    <view v-if="unread > 0" class="unread-badge">
      <text class="unread-text">{{ unreadText }}</text>
    </view>
    <text class="enter-text">进入 ›</text>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useNavbarSafeArea } from '@/composables/useNavbarSafeArea'
import type { RescueMineItem } from '@/types/rescue-session'

const props = defineProps<{
  readonly item: RescueMineItem | null
  readonly unread: number
  /** 主页已有求助卡片时向下错位，避免遮挡 */
  readonly stacked: boolean
}>()

const emit = defineEmits<{
  (event: 'open'): void
}>()

const { navbarBottom } = useNavbarSafeArea()

const STACKED_OFFSET_PX = 56

const cardStyle = computed(() => ({
  top: `${navbarBottom.value + 12 + (props.stacked ? STACKED_OFFSET_PX : 0)}px`,
}))

const unreadText = computed(() => (props.unread > 99 ? '99+' : String(props.unread)))
</script>

<style lang="scss" scoped>
.session-card {
  position: fixed;
  left: $spacing-md;
  z-index: 101;
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  max-width: 520rpx;
  padding: $spacing-sm;
  border-radius: $radius-lg;
  background: $background-white;
  box-shadow: $shadow-md;
}

.pulse-dot {
  flex-shrink: 0;
  width: 16rpx;
  height: 16rpx;
  border-radius: $radius-full;
  background: $primary;
  animation: pulse $duration-cycle infinite;
}

.card-main {
  min-width: 0;
}

.card-title {
  display: block;
  color: $text-primary;
  font-size: $font-size-sm;
  font-weight: $font-weight-semibold;
}

.card-sub {
  @include text-ellipsis;
  display: block;
  max-width: 300rpx;
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
