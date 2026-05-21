<template>
  <scroll-view class="help-history" :class="themeClass" scroll-y>
    <view v-if="helpStore.loadingList" class="state-line">
      <text class="state-text">加载中...</text>
    </view>

    <view v-else-if="!helpStore.helpList.length" class="state-line">
      <text class="state-text">暂无求助记录</text>
    </view>

    <view v-else class="history-list">
      <view
        v-for="item in helpStore.helpList"
        :key="item.id"
        class="history-item"
        @click="openDetail(item.id)"
      >
        <view class="item-main">
          <view class="item-title-row">
            <text class="item-title">{{ item.requestNo }}</text>
            <text class="status-tag" :class="statusClass(item.status)">
              {{ statusText(item.status) }}
            </text>
          </view>
          <text class="item-address">{{ item.address }}</text>
          <text class="item-content">{{ item.content }}</text>
        </view>
        <text class="item-time">{{ formatTime(item.createdAt) }}</text>
      </view>
    </view>

    <view class="safe-bottom" />
  </scroll-view>
</template>

<script setup lang="ts">
import { onShow } from '@dcloudio/uni-app'
import { useTheme } from '@/composables/useTheme'
import { useHelpStore } from '@/store/help'
import type { HelpStatus } from '@/types/help'
import { getHelpStatusClass, getHelpStatusText } from '@/utils/help-labels'

const helpStore = useHelpStore()
const { themeClass } = useTheme()

onShow(() => {
  helpStore.loadList().catch(() => {
    uni.showToast({ title: '加载失败', icon: 'none' })
  })
})

function openDetail(id: string): void {
  uni.navigateTo({ url: `/pages-sub/help/detail?id=${id}` })
}

function statusText(status: HelpStatus): string {
  return getHelpStatusText(status)
}

function statusClass(status: HelpStatus): string {
  return getHelpStatusClass(status)
}

function formatTime(value: string): string {
  return new Date(value).toLocaleString()
}
</script>

<style lang="scss" scoped>
.help-history {
  min-height: 100vh;
  padding: $spacing-md;
  background: $background;
  box-sizing: border-box;
}

.state-line {
  padding: $spacing-xl 0;
  text-align: center;
}

.state-text {
  color: $text-secondary;
  font-size: $font-size-base;
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: $spacing-md;
}

.history-item {
  padding: $spacing-md;
  border-radius: $radius-lg;
  background: $background-white;
  box-shadow: $shadow-sm;
}

.item-title-row {
  @include flex-between;
  gap: $spacing-sm;
}

.item-title {
  color: $text-primary;
  font-size: $font-size-lg;
  font-weight: $font-weight-bold;
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

.item-address,
.item-content,
.item-time {
  display: block;
  margin-top: $spacing-xs;
  color: $text-secondary;
  font-size: $font-size-sm;
  line-height: $line-height-normal;
}

.item-address,
.item-content {
  @include text-ellipsis;
}

.item-time {
  color: $text-tertiary-a11y;
}

.safe-bottom {
  height: calc(#{$spacing-xl} + env(safe-area-inset-bottom));
}
</style>
