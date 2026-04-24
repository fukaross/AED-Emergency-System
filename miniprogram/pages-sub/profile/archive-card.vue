<template>
  <view
    class="ar-card"
    :class="{ 'ar-card--self': isSelf, 'ar-card--clickable': canViewFullPhone }"
    @tap="emit('select', item)"
  >
    <view class="ar-card-top">
      <text class="ar-card-name">{{ item.name }}</text>
      <level-badge :level="item.level" />
    </view>
    <view class="ar-card-row">
      <image class="ar-card-icon" :src="PHONE_ICON" mode="aspectFit" />
      <text class="ar-card-value">{{ item.phone }}</text>
    </view>
    <view class="ar-card-row">
      <image class="ar-card-icon" :src="COURSE_ICON" mode="aspectFit" />
      <text class="ar-card-value">{{ item.college }}</text>
    </view>
    <view class="ar-card-row">
      <image class="ar-card-icon" :src="CALANDER_ICON" mode="aspectFit" />
      <text
        class="ar-card-value"
        :class="{ 'ar-card-value--expired': item.certExpired }"
      >
        {{ formatExpiry(item.certExpiry) }}
      </text>
      <text v-if="item.certExpired" class="ar-card-expired-tag">已过期</text>
    </view>
  </view>
</template>

<script setup lang="ts">
import LevelBadge from '@/components/level-badge/index.vue'
import type { ArchiveItem } from '@/types/user'

const PHONE_ICON = '/static/icons/archive-phone.svg'
const COURSE_ICON = '/static/icons/tab-course.svg'
const CALANDER_ICON = '/static/icons/calander.svg'

interface Props {
  readonly item: ArchiveItem
  readonly isSelf?: boolean
  readonly canViewFullPhone?: boolean
}

defineProps<Props>()

const emit = defineEmits<{
  select: [item: ArchiveItem]
}>()

function formatExpiry(expiry: string): string {
  if (!expiry) return '无有效期'
  return `有效期至 ${expiry.slice(0, 7).replace('-', '年')}月`
}
</script>

<style lang="scss" scoped>
.ar-card {
  margin: 0 $spacing-md $spacing-md;
  padding: 32rpx;
  background: $background-white;
  border-radius: $radius-lg;
  box-shadow: $shadow-sm;
}

.ar-card--clickable:active {
  opacity: 0.76;
}

.ar-card--self {
  border: 2rpx solid $primary;
  background: linear-gradient(135deg, rgba($primary, 0.04) 0%, $background-white 100%);
}

.ar-card-top {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  margin-bottom: $spacing-md;
}

.ar-card-name {
  font-size: 34rpx;
  font-weight: $font-weight-semibold;
  color: $text-primary;
}

.ar-card-row {
  display: flex;
  align-items: center;
  margin-top: $spacing-sm;
}

.ar-card-icon {
  width: 36rpx;
  height: 36rpx;
  margin-right: $spacing-sm;
  flex-shrink: 0;
}

.ar-card-value {
  font-size: $font-size-base;
  color: $text-secondary;
}

.ar-card-value--expired {
  color: $status-error;
}

.ar-card-expired-tag {
  margin-left: $spacing-xs;
  height: 32rpx;
  padding: 0 10rpx;
  border-radius: 6rpx;
  background: $background-card-disabled;
  font-size: $font-size-xs;
  color: $text-tertiary;
  display: flex;
  align-items: center;
}
</style>
