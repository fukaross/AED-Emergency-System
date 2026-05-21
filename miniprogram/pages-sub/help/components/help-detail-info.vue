<template>
  <view class="detail-content">
    <view class="title-row">
      <text class="title-text">求助 {{ detail.requestNo }}</text>
      <text class="status-chip" :class="statusClass">{{ statusText }}</text>
    </view>

    <view class="info-list">
      <view class="info-row">
        <text class="info-label">电话</text>
        <view class="phone-action" @click="$emit('call-requester')">
          <text class="info-value">{{ detail.requesterPhone }}</text>
          <text class="phone-icon">☎</text>
        </view>
      </view>
      <view class="info-row">
        <text class="info-label">地点</text>
        <text class="info-value">{{ detail.address }}</text>
      </view>
      <view class="info-row">
        <text class="info-label">内容</text>
        <text class="info-value">{{ detail.content }}</text>
      </view>
      <view class="info-row">
        <text class="info-label">时间</text>
        <text class="info-value">{{ formatTime(detail.createdAt) }}</text>
      </view>
    </view>

    <view v-if="detail.images.length" class="image-strip">
      <image
        v-for="(image, index) in detail.images"
        :key="image"
        class="scene-image"
        :src="image"
        mode="aspectFill"
        @click="$emit('preview-scene', index)"
      />
    </view>

    <view class="timeline">
      <view
        v-for="entry in detail.timeline"
        :key="`${entry.action}-${entry.timestamp}`"
        class="timeline-row"
      >
        <text class="timeline-dot" />
        <text class="timeline-text">
          {{ actionText(entry.action) }} · {{ formatTime(entry.timestamp) }}
        </text>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { HelpRequestDetail } from '@/types/help'
import {
  getHelpActionText,
  getHelpStatusClass,
  getHelpStatusText,
} from '@/utils/help-labels'
import { formatChineseDateTime } from '@/utils/format'

const props = defineProps<{
  readonly detail: HelpRequestDetail
}>()

defineEmits<{
  (event: 'call-requester'): void
  (event: 'preview-scene', index: number): void
}>()

const statusText = computed(() => getHelpStatusText(props.detail.status))
const statusClass = computed(() => getHelpStatusClass(props.detail.status))

function formatTime(value: string): string {
  return formatChineseDateTime(value)
}

function actionText(action: string): string {
  return getHelpActionText(action)
}
</script>

<style lang="scss" scoped>
.detail-content {
  padding: $spacing-md;
}

.title-row,
.info-row,
.phone-action {
  @include flex-align-center;
}

.title-row {
  justify-content: space-between;
  margin-bottom: $spacing-md;
}

.title-text {
  color: $text-primary;
  font-size: $font-size-xl;
  font-weight: $font-weight-bold;
}

.status-chip {
  padding: 6rpx 18rpx;
  border-radius: $radius-pill;
  color: $text-white;
  font-size: $font-size-xs;
}

.status-chip--published,
.status-chip--responding {
  background: $status-info;
}

.status-chip--pending_review {
  background: $status-warning;
}

.status-chip--closed {
  background: $status-online;
}

.status-chip--false_alarm {
  background: $status-offline;
}

.info-row {
  align-items: flex-start;
  gap: $spacing-md;
  padding: $spacing-sm 0;
  border-bottom: 1rpx solid $divider-light;
}

.info-label {
  width: 80rpx;
  color: $text-tertiary-a11y;
  font-size: $font-size-sm;
}

.info-value {
  flex: 1;
  color: $text-primary;
  font-size: $font-size-base;
  line-height: $line-height-normal;
}

.phone-action {
  flex: 1;
  gap: $spacing-xs;
}

.phone-icon {
  width: 36rpx;
  color: $primary;
  font-size: $font-size-lg;
  line-height: 36rpx;
  text-align: center;
}

.image-strip {
  display: flex;
  gap: $spacing-sm;
  margin-top: $spacing-md;
}

.scene-image {
  width: 144rpx;
  height: 144rpx;
  border-radius: $radius-md;
}

.timeline {
  margin-top: $spacing-md;
}

.timeline-row {
  @include flex-align-center;
  gap: $spacing-sm;
  padding: $spacing-xs 0;
}

.timeline-dot {
  width: 12rpx;
  height: 12rpx;
  border-radius: $radius-full;
  background: $primary;
}

.timeline-text {
  color: $text-secondary;
  font-size: $font-size-sm;
}
</style>
