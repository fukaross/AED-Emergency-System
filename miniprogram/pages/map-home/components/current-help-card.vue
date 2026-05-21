<template>
  <view v-if="request" class="current-help" :style="cardStyle" @click="emit('open')">
    <view class="card-main">
      <text class="card-no">{{ request.requestNo }}</text>
      <text class="card-address">{{ request.address }}</text>
    </view>
    <text class="status-tag" :class="statusClass">{{ statusText }}</text>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useNavbarSafeArea } from '@/composables/useNavbarSafeArea'
import type { HelpListItem } from '@/types/help'
import { getHelpStatusClass, getHelpStatusText } from '@/utils/help-labels'

const props = defineProps<{
  readonly request: HelpListItem | null
}>()

const emit = defineEmits<{
  (event: 'open'): void
}>()

const { navbarBottom } = useNavbarSafeArea()

const cardStyle = computed(() => ({
  top: `${navbarBottom.value + 12}px`,
}))

const statusText = computed(() => {
  return props.request ? getHelpStatusText(props.request.status) : ''
})

const statusClass = computed(() => {
  return getHelpStatusClass(props.request?.status)
})
</script>

<style lang="scss" scoped>
.current-help {
  position: fixed;
  left: $spacing-md;
  z-index: 101;
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  max-width: 460rpx;
  padding: $spacing-sm;
  border-radius: $radius-lg;
  background: $background-white;
  box-shadow: $shadow-md;
}

.card-main {
  min-width: 0;
}

.card-no {
  display: block;
  color: $text-primary;
  font-size: $font-size-sm;
  font-weight: $font-weight-semibold;
}

.card-address {
  @include text-ellipsis;
  display: block;
  max-width: 300rpx;
  margin-top: 4rpx;
  color: $text-secondary;
  font-size: $font-size-xs;
}

.status-tag {
  flex-shrink: 0;
  padding: 4rpx 12rpx;
  border-radius: $radius-pill;
  color: $text-white;
  font-size: $font-size-xs;
}

.status-tag--published,
.status-tag--responding {
  background: $status-info;
}

.status-tag--pending_review {
  background: $status-warning;
}

.status-tag--closed {
  background: $status-online;
}

.status-tag--false_alarm {
  background: $status-offline;
}
</style>
