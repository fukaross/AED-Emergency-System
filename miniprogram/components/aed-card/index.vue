<template>
  <view
    class="aed-card"
    :class="{ 'aed-card--disabled': !isOnline }"
  >
    <view class="aed-card-image" @click="previewImage">
      <image
        class="aed-card-img"
        :src="image"
        mode="aspectFill"
        lazy-load
      />
    </view>
    <view
      class="aed-card-info"
      @click="handleInfoClick"
    >
      <text class="aed-card-name">{{ name }}</text>
      <text class="aed-card-distance">距离 {{ formattedDistance }}</text>
      <status-tag :online="isOnline" />
    </view>
    <view class="aed-card-action" @click="handleNavigate">
      <image
        class="aed-card-nav-icon"
        :src="LOCATION_ICON"
        mode="aspectFit"
      />
      <text class="aed-card-nav-text">查看</text>
    </view>
  </view>
</template>

<script setup lang="ts">
// AED 设备卡片 —— 可用=白底红标可点击，不可用=灰底灰标不可点
import { computed } from 'vue'
import StatusTag from '@/components/status-tag/index.vue'

const LOCATION_ICON = '/static/icons/location.svg'

interface Props {
  id: string
  name: string
  distance?: number
  status: 'online' | 'offline'
  image: string
}

const props = defineProps<Props>()

const emit = defineEmits<{
  detail: [id: string]
  navigate: [id: string]
}>()

const isOnline = computed(() => props.status === 'online')

const formattedDistance = computed(() => {
  const distance = props.distance ?? 0
  if (distance <= 0) {
    return '距离未知'
  }
  if (distance >= 1000) {
    return `${(distance / 1000).toFixed(1)}km`
  }
  return `${Math.round(distance)}m`
})

function previewImage() {
  uni.previewImage({ urls: [props.image], current: props.image })
}

function handleInfoClick() {
  if (isOnline.value) {
    emit('detail', props.id)
  }
}

function handleNavigate() {
  emit('navigate', props.id)
}
</script>

<style lang="scss" scoped>
.aed-card {
  @include card-base;
  @include flex-align-center;
  padding: $spacing-md;
  margin: 0 $spacing-md $spacing-sm;
}

.aed-card--disabled {
  background-color: $background-card-disabled;
}

.aed-card-image {
  flex-shrink: 0;
  width: 160rpx;
  height: 160rpx;
  border-radius: $radius-md;
  overflow: hidden;
}

.aed-card-img {
  width: 100%;
  height: 100%;
}

.aed-card-info {
  flex: 1;
  padding: 0 $spacing-sm;
  display: flex;
  flex-direction: column;
  gap: $spacing-xs;
}

.aed-card-name {
  font-size: $font-size-lg;
  font-weight: $font-weight-semibold;
  color: $text-primary;
  @include text-ellipsis;
}

.aed-card-distance {
  font-size: $font-size-sm;
  color: $text-tertiary;
}

.aed-card-action {
  flex-shrink: 0;
  @include flex-center;
  flex-direction: column;
  gap: 4rpx;
  padding: $spacing-xs;
}

.aed-card-nav-icon {
  width: 48rpx;
  height: 48rpx;
}

.aed-card-nav-text {
  font-size: $font-size-xs;
  color: $link-blue;
}
</style>
