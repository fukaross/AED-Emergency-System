<template>
  <view
    class="rescuer-card"
    :class="{
      'rescuer-card--online': isOnline,
      'rescuer-card--offline': !isOnline,
    }"
  >
    <!-- 左侧头像 -->
    <view class="card-avatar">
      <image
        class="card-avatar-img"
        :src="item.avatarUrl || DEFAULT_AVATAR"
        mode="aspectFill"
      />
    </view>

    <!-- 中间信息区 -->
    <view
      class="card-info"
      :class="{ 'card-info--clickable': isOnline }"
      @click="handleInfoClick"
    >
      <view class="card-name-row">
        <text class="card-name">{{ item.name }}</text>
        <LevelBadge v-if="item.level" :level="item.level" />
      </view>
      <view class="card-meta">
        <view class="card-status">
          <image
            class="status-dot"
            :src="isOnline ? ONLINE_ICON : OFFLINE_ICON"
            mode="aspectFit"
          />
          <text class="status-text" :class="`status-text--${item.availabilityStatus}`">
            {{ isOnline ? '在线' : '离线' }}
          </text>
        </view>
        <RescuerSkillBadges :skills="item.skills" />
      </view>
    </view>

    <!-- 右侧呼叫区 -->
    <view
      class="card-action"
      :class="{ 'card-action--offline': !isOnline }"
      @click="handleCall"
    >
      <image
        class="call-icon"
        :src="isOnline ? PHONE_ICON : PHONE_OFFLINE_ICON"
        mode="aspectFit"
      />
      <text class="call-text" :class="{ 'call-text--offline': !isOnline }">呼叫</text>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { RescuerListItem } from '@/types/rescuer'
import LevelBadge from '@/components/level-badge/index.vue'
import RescuerSkillBadges from './rescuer-skill-badges.vue'

const DEFAULT_AVATAR = '/static/images/avatar-default.png'
const ONLINE_ICON = '/static/icons/online.svg'
const OFFLINE_ICON = '/static/icons/offline.svg'
const PHONE_ICON = '/static/icons/phone.svg'
const PHONE_OFFLINE_ICON = '/static/icons/phone-offline.svg'

interface Props {
  item: RescuerListItem
}

const props = defineProps<Props>()

const emit = defineEmits<{
  detail: [id: string]
  call: [rescuerId: string]
}>()

const isOnline = computed(() => props.item.availabilityStatus === 'online')

function handleInfoClick() {
  if (isOnline.value) {
    emit('detail', props.item.id)
  }
}

function handleCall() {
  emit('call', props.item.id)
}
</script>

<style lang="scss" scoped>
.rescuer-card {
  display: flex;
  align-items: center;
  margin: 0 $spacing-md $spacing-md;
  padding: $spacing-md $spacing-lg;
  border-radius: $radius-lg;
  background: $background-white;
  box-shadow: $shadow-sm;
}

.rescuer-card--offline {
  background: $background-card-disabled;
}

.card-avatar {
  flex-shrink: 0;
  margin-right: $spacing-md;
}

.card-avatar-img {
  width: 112rpx;
  height: 112rpx;
  border-radius: $radius-full;
  background: $background;
}

.card-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: $spacing-sm;
}

.card-info--clickable:active {
  opacity: 0.7;
}

.card-name-row {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
}

.card-name {
  font-size: $font-size-xl;
  font-weight: $font-weight-semibold;
  color: $text-primary;
}

.card-meta {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  flex-wrap: wrap;
}

.card-status {
  display: flex;
  align-items: center;
  gap: 6rpx;
}

.status-dot {
  width: 28rpx;
  height: 28rpx;
}

.status-text {
  font-size: $font-size-base;
}

.status-text--online {
  color: $status-online;
}

.status-text--offline {
  color: $status-offline;
}

.card-action {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  margin-left: $spacing-md;
  padding: $spacing-sm $spacing-md;
  min-width: 96rpx;
}

.call-icon {
  width: 52rpx;
  height: 52rpx;
  margin-bottom: 6rpx;
}

.call-text {
  font-size: $font-size-sm;
  color: $primary;
}

.call-text--offline {
  color: $status-offline;
}
</style>
