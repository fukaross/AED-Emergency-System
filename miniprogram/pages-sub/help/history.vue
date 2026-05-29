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
            <view class="title-group">
              <text v-if="roleText(item.userRole)" class="role-tag" :class="roleClass(item.userRole)">
                {{ roleText(item.userRole) }}
              </text>
              <text class="item-title">{{ item.requestNo }}</text>
            </view>
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
import { onHide, onPullDownRefresh, onShow, onUnload } from '@dcloudio/uni-app'
import { useTheme } from '@/composables/useTheme'
import { useHelpStore } from '@/store/help'
import type { HelpStatus } from '@/types/help'
import { formatChineseDateTime } from '@/utils/format'
import { getHelpStatusClass, getHelpStatusText, getHelpUserRoleClass, getHelpUserRoleText } from '@/utils/help-labels'

const POLL_INTERVAL_MS = 20_000

const helpStore = useHelpStore()
const { themeClass } = useTheme()

let pollTimer: ReturnType<typeof setInterval> | null = null
let isPolling = false

async function pollList() {
  if (isPolling) return
  isPolling = true
  try { await helpStore.loadList(1, 20, true) }
  catch { /* silently ignore poll errors */ }
  finally { isPolling = false }
}

function startPolling() {
  stopPolling()
  pollTimer = setInterval(pollList, POLL_INTERVAL_MS)
}

function stopPolling() {
  if (pollTimer) { clearInterval(pollTimer); pollTimer = null }
  isPolling = false
}

onShow(() => {
  helpStore.loadList().catch(() => {
    uni.showToast({ title: '加载失败', icon: 'none' })
  })
  startPolling()
})

onHide(() => { stopPolling() })

onUnload(() => { stopPolling() })

onPullDownRefresh(async () => {
  try { await helpStore.loadList() }
  catch { /* ignore */ }
  finally { uni.stopPullDownRefresh() }
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

function roleText(role: string): string {
  return getHelpUserRoleText(role)
}

function roleClass(role: string): string {
  return getHelpUserRoleClass(role)
}

function formatTime(value: string): string {
  return formatChineseDateTime(value)
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

.title-group {
  @include flex-align-center;
  gap: $spacing-xs;
  flex: 1;
  min-width: 0;
}

.role-tag {
  flex-shrink: 0;
  padding: 2rpx 8rpx;
  border-radius: $radius-pill;
  font-size: $font-size-xs;
}

.role-tag--requester {
  color: $status-info;
  background: rgba($status-info, 0.08);
}

.role-tag--reporter {
  color: $status-online;
  background: rgba($status-online, 0.08);
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
