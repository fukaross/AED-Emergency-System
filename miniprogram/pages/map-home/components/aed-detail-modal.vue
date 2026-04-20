<template>
  <Modal
    v-model="visible"
    title="AED 设备信息"
    cancel-text="关闭"
    confirm-text="导航前往"
    @confirm="handleConfirm"
  >
    <view v-if="selectedAed" class="aed-detail">
      <image
        v-if="selectedAed.image"
        class="aed-detail__image"
        :src="selectedAed.image"
        mode="aspectFill"
        @tap="handlePreviewImage"
      />
      <text class="aed-detail__name">{{ selectedAed.name }}</text>
      <view v-if="selectedAed.address" class="aed-detail__address">
        <view class="aed-detail__address-icon" />
        <text class="aed-detail__address-text">{{ selectedAed.address }}</text>
      </view>
      <view class="aed-detail__status-row">
        <view
          class="aed-detail__dot"
          :class="selectedAed.status === 'online' ? 'dot--online' : 'dot--offline'"
        />
        <text class="aed-detail__status-text">
          {{ selectedAed.status === 'online' ? '在线可用' : '离线不可用' }}
        </text>
        <text class="aed-detail__distance">
          {{ formatDistance(selectedAed.distance ?? 0) }}
        </text>
      </view>
    </view>
  </Modal>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import Modal from '@/components/modal/index.vue'
import { formatDistance } from '@/services/map-service'
import type { AedItem } from '@/types/aed'

interface Props {
  readonly modelValue: boolean
  readonly selectedAed: AedItem | null
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirm: []
  'preview-image': []
}>()

const visible = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value),
})

function handleConfirm(): void {
  emit('confirm')
}

function handlePreviewImage(): void {
  emit('preview-image')
}
</script>

<style lang="scss" scoped>
.aed-detail {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: $spacing-sm;

  &__image {
    width: 100%;
    height: 300rpx;
    border-radius: $radius-md;
    background-color: $background;
  }

  &__name {
    font-size: $font-size-xl;
    font-weight: $font-weight-bold;
    color: $text-primary;
    text-align: center;
  }

  &__address {
    display: flex;
    align-items: flex-start;
    max-width: 100%;
  }

  &__address-icon {
    width: 28rpx;
    height: 28rpx;
    flex-shrink: 0;
    margin-top: 2rpx;
    margin-right: $spacing-xs;
    background-image: url('/static/icons/AED-location@3x.png');
    background-position: center;
    background-repeat: no-repeat;
    background-size: contain;
  }

  &__address-text {
    font-size: $font-size-sm;
    color: $text-secondary;
    line-height: $line-height-normal;
    @include multi-line-ellipsis(2);
  }

  &__status-row {
    @include flex-align-center;
    gap: $spacing-xs;
  }

  &__dot {
    width: 16rpx;
    height: 16rpx;
    border-radius: $radius-full;
    flex-shrink: 0;
  }

  &__status-text {
    font-size: $font-size-base;
    color: $text-primary;
  }

  &__distance {
    font-size: $font-size-base;
    color: $text-tertiary;
    margin-left: $spacing-xs;
  }
}

.dot--online {
  background-color: $status-online;
}

.dot--offline {
  background-color: $status-offline;
}
</style>
