<template>
  <BottomSheet
    v-model="visible"
    title=""
    max-height="50vh"
    :respect-tab-bar="true"
  >
    <view class="nearby-header">
      <text class="nearby-header__title">
        附近AED设备 (共{{ aeds.length }}台)
      </text>
    </view>
    <view
      v-for="aed in aeds"
      :key="aed.id"
      class="nearby-item"
    >
      <view class="nearby-item__left">
        <view
          class="nearby-item__dot"
          :class="aed.status === 'online' ? 'dot--online' : 'dot--offline'"
        />
        <view class="nearby-item__info">
          <text class="nearby-item__name">{{ aed.name }}</text>
          <text class="nearby-item__distance">
            {{ formatDistanceText(aed.distance) }}
          </text>
        </view>
      </view>
      <view class="nearby-item__right">
        <text
          class="nearby-item__status"
          :class="aed.status === 'online' ? 'status--online' : 'status--offline'"
        >
          {{ aed.status === 'online' ? '可用' : '不可用' }}
        </text>
        <view class="nearby-item__nav" @click="handleNavigate(aed)">
          <text class="nearby-item__nav-text">导航</text>
        </view>
      </view>
    </view>
  </BottomSheet>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import BottomSheet from '@/components/bottom-sheet/index.vue'
import type { AedItem } from '@/types/aed'

interface Props {
  readonly modelValue: boolean
  readonly aeds: readonly AedItem[]
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  navigate: [aed: AedItem]
}>()

const visible = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value),
})

function formatDistanceText(distance?: number): string {
  return distance && distance > 0 ? `${distance}m` : '距离未知'
}

function handleNavigate(aed: AedItem): void {
  emit('navigate', aed)
}
</script>

<style lang="scss" scoped>
.nearby-header {
  padding-bottom: $spacing-sm;

  &__title {
    font-size: $font-size-lg;
    font-weight: $font-weight-semibold;
    color: $text-primary;
  }
}

.nearby-item {
  @include flex-between;
  padding: $spacing-sm 0;
  border-bottom: 1rpx solid $divider-light;

  &:last-child {
    border-bottom: none;
  }

  &__left {
    @include flex-align-center;
    flex: 1;
    min-width: 0;
  }

  &__dot {
    width: 16rpx;
    height: 16rpx;
    border-radius: $radius-full;
    margin-right: $spacing-sm;
    flex-shrink: 0;
  }

  &__info {
    flex: 1;
    min-width: 0;
  }

  &__name {
    display: block;
    font-size: $font-size-base;
    font-weight: $font-weight-medium;
    color: $text-primary;
    @include text-ellipsis;
  }

  &__distance {
    display: block;
    margin-top: 4rpx;
    font-size: $font-size-sm;
    color: $text-tertiary;
  }

  &__right {
    @include flex-align-center;
    flex-shrink: 0;
    margin-left: $spacing-sm;
  }

  &__status {
    margin-right: $spacing-sm;
    padding: 0 12rpx;
    height: 36rpx;
    line-height: 36rpx;
    border-radius: $radius-sm;
    font-size: $font-size-xs;
  }

  &__nav {
    @include btn-mini;
    background-color: $background-white;
    border: 1rpx solid $navigation-blue;
    color: $navigation-blue;
  }

  &__nav-text {
    font-size: $font-size-sm;
    color: $navigation-blue;
  }
}

.dot--online {
  background-color: $status-online;
}

.dot--offline {
  background-color: $status-offline;
}

.status--online {
  background-color: rgba(76, 175, 80, 0.1);
  color: $status-online;
}

.status--offline {
  background-color: rgba(158, 158, 158, 0.1);
  color: $status-offline;
}
</style>
