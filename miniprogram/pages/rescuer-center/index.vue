<template>
  <view class="rescuer-center" :class="themeClass">
    <!-- 登录锁页态 -->
    <RescuerLoginLock v-if="!authStore.isLoggedIn" @login="handleLogin" />

    <!-- 已登录 -->
    <template v-else>
      <!-- 加载态 -->
      <view v-if="rescuerStore.loading || showInitialLoading" class="rc-loading">
        <text class="rc-loading-text">正在加载附近急救员...</text>
      </view>

      <!-- 加载失败态 -->
      <view v-else-if="showLoadError" class="rc-error">
        <text class="rc-error-title">加载失败</text>
        <text class="rc-error-text">{{ rescuerStore.loadError }}</text>
        <view class="rc-error-btn" @click="loadRescuers">
          <text>重试</text>
        </view>
      </view>

      <!-- 空态 -->
      <RescuerEmptyState
        v-else-if="showEmpty"
        @guide="goGuide"
        @call120="call120"
      />

      <!-- 正常列表态 -->
      <view v-else class="rc-list">
        <!-- 在线急救员 -->
        <view v-if="onlineItems.length > 0" class="rc-section">
          <view class="rc-section-header">
            <image
              class="rc-section-icon"
              :src="ACTIVE_BANNER"
              mode="aspectFit"
            />
            <text class="rc-section-title">在线急救员</text>
          </view>
          <RescuerListItem
            v-for="item in onlineItems"
            :key="item.id"
            :item="item"
            @detail="handleDetail"
            @call="handleCallFromList"
          />
        </view>

        <!-- 离线急救员 -->
        <view v-if="offlineItems.length > 0" class="rc-section">
          <view class="rc-section-header">
            <image
              class="rc-section-icon"
              :src="INACTIVE_BANNER"
              mode="aspectFit"
            />
            <text class="rc-section-title">离线急救员</text>
          </view>
          <RescuerListItem
            v-for="item in offlineItems"
            :key="item.id"
            :item="item"
            @detail="handleDetail"
            @call="handleCallFromList"
          />
        </view>

        <!-- 两栏都为空时的局部提示（排除全局空态已拦截的情况） -->
        <view
          v-if="onlineItems.length === 0 && offlineItems.length === 0"
          class="rc-section-empty"
        >
          <text>暂无急救员数据</text>
        </view>

        <view class="rc-spacer" />
      </view>
    </template>

    <!-- 急救员详情弹窗 -->
    <RescuerDetailModal
      :model-value="rescuerStore.showDetail"
      :detail="rescuerStore.currentDetail"
      :name="detailName"
      @update:model-value="handleDetailVisibleChange"
      @close="rescuerStore.closeDetail"
      @after-close="handleDetailAfterClose"
      @call="handleCallFromDetail"
    />

    <AppTabBar current-path="/pages/rescuer-center/index" />
  </view>
</template>

<script setup lang="ts">
/**
 * 急救员中心页面
 *
 * 三种界面态：登录锁页、正常列表、空态
 * 数据入口：app/api/rescuer.ts
 */
import { computed, ref } from 'vue'
import { onPullDownRefresh, onShow } from '@dcloudio/uni-app'

const ACTIVE_BANNER = '/static/icons/rescuer-active.svg'
const INACTIVE_BANNER = '/static/icons/rescuer-inactive.svg'

import { useTheme } from '@/composables/useTheme'
import { useAuthStore } from '@/store/auth'
import { useRescuerStore } from '@/store/rescuer'
import { getCurrentLocation } from '@/services/geo-service'
import { auditCallResponder, auditViewContact } from '@/api/audit'
import { dialPhone } from '@/utils/dial'
import AppTabBar from '@/components/app-tab-bar/index.vue'
import RescuerDetailModal from './components/rescuer-detail-modal.vue'
import RescuerEmptyState from './components/rescuer-empty-state.vue'
import RescuerListItem from './components/rescuer-list-item.vue'
import RescuerLoginLock from './components/rescuer-login-lock.vue'

const authStore = useAuthStore()
const rescuerStore = useRescuerStore()
const { themeClass } = useTheme()
const detailName = ref('')

const listItems = computed(() => rescuerStore.listResponse?.items ?? [])
const onlineItems = computed(() =>
  listItems.value.filter((i) => i.availabilityStatus === 'online'),
)
const offlineItems = computed(() =>
  listItems.value.filter((i) => i.availabilityStatus === 'offline'),
)
const showLoadError = computed(
  () =>
    !rescuerStore.loading &&
    !!rescuerStore.loadError &&
    listItems.value.length === 0,
)
const showInitialLoading = computed(
  () =>
    !rescuerStore.loading &&
    !rescuerStore.loadError &&
    !rescuerStore.listResponse,
)

const showEmpty = computed(() => {
  if (rescuerStore.loading || showLoadError.value) return false

  const response = rescuerStore.listResponse
  if (!response) return false
  if (response.items.length === 0) return true
  if (response.viewerCampusDistance > response.autoOfflineRadius) return true

  return false
})

async function handleLogin() {
  await authStore.login()
  await loadRescuers()
}

async function loadRescuers() {
  if (!authStore.isLoggedIn) return

  const location = await getCurrentLocation()
  if (!location.success && location.errorMessage) {
    uni.showToast({ title: location.errorMessage, icon: 'none' })
  }

  try {
    await rescuerStore.loadNearbyRescuers(location.point.lat, location.point.lng)
  } catch {
    // 请求层 toast 与页面错误态会继续兜底
  }
}

async function handleDetail(id: string) {
  detailName.value = getNameById(id)

  try {
    await rescuerStore.openRescuerDetail(id)
    auditViewContact(id)
  } catch {
    detailName.value = ''
    // 错误已在 store 中提示
  }
}

async function handleCallFromList(rescuerId: string) {
  try {
    const detail = await rescuerStore.loadRescuerDetail(rescuerId)
    makeCall(detail.phone, rescuerId)
  } catch {
    uni.showToast({ title: '加载详情失败', icon: 'none' })
  }
}

function handleCallFromDetail() {
  const detail = rescuerStore.currentDetail
  if (!detail) return

  makeCall(detail.phone, detail.id)
}

function handleDetailVisibleChange(visible: boolean) {
  if (!visible) {
    rescuerStore.closeDetail()
  }
}

function handleDetailAfterClose() {
  rescuerStore.clearDetail()
  detailName.value = ''
}

function getNameById(rescuerId: string): string {
  return listItems.value.find((item) => item.id === rescuerId)?.name ?? ''
}

function makeCall(phone: string, rescuerId: string) {
  auditCallResponder(rescuerId)
  dialPhone(phone, {
    onFail: () => {
      uni.showModal({
        title: '无法直接拨打',
        content: `请手动拨打: ${phone}`,
        confirmText: '复制号码',
        success: (result) => {
          if (result.confirm) {
            uni.setClipboardData({
              data: phone,
              success: () => uni.showToast({ title: '号码已复制' }),
            })
          }
        },
      })
    },
  })
}

function call120() {
  dialPhone('120')
}

function goGuide() {
  uni.navigateTo({ url: '/pages-sub/emergency-guide/index' })
}

onShow(() => {
  loadRescuers()
})

onPullDownRefresh(async () => {
  try {
    await loadRescuers()
  } finally {
    uni.stopPullDownRefresh()
  }
})
</script>

<style src="./index.scss" lang="scss" scoped></style>
