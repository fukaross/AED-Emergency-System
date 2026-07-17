<template>
  <view class="map-home" :class="themeClass">
    <CustomNavbar
      title="REDACTED地图"
      :transparent="true"
      title-align="left"
    />

    <map
      id="aedMap"
      class="map-canvas"
      :latitude="mapCenter.latitude"
      :longitude="mapCenter.longitude"
      :scale="mapZoom"
      :min-scale="12"
      :max-scale="20"
      :markers="markers"
      :polyline="polylines"
      :show-location="hasRealLocation"
      @markertap="handleMarkerTap"
      @tap="handleMapTap"
      @error="handleMapError"
    />

    <MapFloatingActions
      :help-disabled="Boolean(helpStore.activeRequest)"
      @open-help="openHelpPage"
      @help-disabled="showActiveHelpTip"
      @toggle-nearby="toggleNearbyPanel"
      @relocate="handleRelocate"
      @nearest="handleNearest"
    />

    <CurrentHelpCard
      :request="helpStore.activeRequest"
      @open="openCurrentHelp"
    />

    <RescueSessionCard
      :item="activeSessionItem"
      :unread="rescueStore.totalUnread"
      :stacked="Boolean(helpStore.activeRequest)"
      @open="openActiveSession"
    />

    <MapStatusOverlay
      :status-message="statusMessage"
      :status-bar-dismissed="statusBarDismissed"
      :show-loading="isLocating || aedStore.loading"
      @dismiss-status="statusBarDismissed = true"
    />

    <NearbyAedPanel
      v-model="showNearbyPanel"
      :aeds="aedStore.sortedAedList"
      @navigate="navigateToAed"
    />

    <AedDetailModal
      v-model="showAedModal"
      :selected-aed="selectedAed"
      @confirm="handleModalNavigate"
      @preview-image="handlePreviewAedImage"
    />

    <Modal
      v-model="showSubscribePrompt"
      title="接收通知"
      :show-close="false"
      :show-footer="false"
    >
      <view class="subscribe-prompt">
        <text class="subscribe-prompt__text">有人求助时提醒你</text>
      </view>
      <template #footer>
        <view class="subscribe-prompt__actions">
          <button
            class="subscribe-prompt__btn subscribe-prompt__btn--ghost"
            @tap="closeSubscribePrompt"
          >
            稍后
          </button>
          <button
            class="subscribe-prompt__btn subscribe-prompt__btn--primary"
            :loading="isRequestingSubscribe"
            @tap="handleEnableSubscribe"
          >
            开启
          </button>
        </view>
      </template>
    </Modal>

    <AppTabBar current-path="/pages/map-home/index" />
  </view>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { onHide, onShow } from '@dcloudio/uni-app'
import { requestAlertSubscribe } from '@/api/wechat-subscribe'
import AppTabBar from '@/components/app-tab-bar/index.vue'
import Modal from '@/components/modal/index.vue'
import CustomNavbar from '@/components/custom-navbar/index.vue'
import { useAedMap } from '@/composables/useAedMap'
import { useTheme } from '@/composables/useTheme'
import {
  createMapCtx,
  openNativeNavigation,
  RIPPLE_ID_BASE,
} from '@/services/map-service'
import { useAedStore } from '@/store/aed'
import { useAuthStore } from '@/store/auth'
import { useHelpStore } from '@/store/help'
import { useRescueSessionStore } from '@/store/rescue-session'
import { useUserStore } from '@/store/user'
import type { AedItem } from '@/types/aed'
import { WECHAT_ALERT_TEMPLATE_ID } from '@/utils/constants'
import { RESCUE_MINE_POLL_INTERVAL_MS } from '@/utils/rescue-signal'
import { USE_REAL_WECHAT_FLOW } from '@/utils/runtime-env'
import AedDetailModal from './components/aed-detail-modal.vue'
import CurrentHelpCard from './components/current-help-card.vue'
import MapFloatingActions from './components/map-floating-actions.vue'
import MapStatusOverlay from './components/map-status-overlay.vue'
import NearbyAedPanel from './components/nearby-aed-panel.vue'
import RescueSessionCard from './components/rescue-session-card.vue'

interface MarkerTapEvent {
  readonly markerId?: number
  readonly detail?: {
    readonly markerId?: number
  }
}

const MAP_ID = 'aedMap'
const MARKER_TAP_GUARD_MS = 250
const MAP_LOAD_ERROR_TEXT = '地图加载失败，请检查网络后重试'
const SUBSCRIBE_PROMPT_DELAY_MS = 800
const HELP_POLL_INTERVAL_MS = 30_000
const AED_POLL_INTERVAL_MS = 60_000

let hasPromptedSubscribeThisSession = false

const aedStore = useAedStore()
const authStore = useAuthStore()
const helpStore = useHelpStore()
const rescueStore = useRescueSessionStore()
const userStore = useUserStore()
const { themeClass } = useTheme()
const {
  mapCenter,
  mapZoom,
  markers,
  polylines,
  locationError,
  isLocating,
  hasRealLocation,
  isNearestActive,
  refreshCurrentAed,
  focusAedById,
  relocate,
  goToNearestAed,
  clearRoute,
  getActiveNearestAed,
} = useAedMap()

const mapErrorMessage = ref('')
const showNearbyPanel = ref(false)
const statusBarDismissed = ref(false)
const showAedModal = ref(false)
const showSubscribePrompt = ref(false)
const isRequestingSubscribe = ref(false)
const selectedAed = ref<AedItem | null>(null)

let lastMarkerTapAt = 0
let subscribePromptTimer: ReturnType<typeof setTimeout> | null = null
let helpPollTimer: ReturnType<typeof setInterval> | null = null
let aedPollTimer: ReturnType<typeof setInterval> | null = null
let sessionPollTimer: ReturnType<typeof setInterval> | null = null
let isHelpPolling = false
let isAedPolling = false
let isSessionPolling = false

onMounted(() => {
  createMapCtx(MAP_ID)
})

onShow(() => {
  pollAedList()
  helpStore.loadList().catch(() => {
    console.warn('[MapHome] 求助列表加载失败')
  })
  void pollMineSessions()
  startSessionPolling()
  scheduleSubscribePrompt()
  startAedPolling()
  startHelpPolling()
})

onHide(() => {
  clearSubscribePromptTimer()
  stopAedPolling()
  stopHelpPolling()
  stopSessionPolling()
})

onUnmounted(() => {
  clearSubscribePromptTimer()
  stopAedPolling()
  stopHelpPolling()
  stopSessionPolling()
})

const statusMessage = computed(() => {
  if (mapErrorMessage.value) return mapErrorMessage.value
  if (locationError.value) return locationError.value
  if (aedStore.error) return aedStore.error
  return ''
})

async function pollAedList(): Promise<void> {
  if (isAedPolling) return
  isAedPolling = true
  try {
    await refreshCurrentAed()
  } catch {
    console.warn('[MapHome] AED 列表刷新失败')
  } finally {
    isAedPolling = false
  }
}

function startAedPolling(): void {
  stopAedPolling()
  aedPollTimer = setInterval(pollAedList, AED_POLL_INTERVAL_MS)
}

function stopAedPolling(): void {
  if (aedPollTimer) {
    clearInterval(aedPollTimer)
    aedPollTimer = null
  }
  isAedPolling = false
}

async function pollHelpList() {
  if (isHelpPolling) return
  isHelpPolling = true
  try { await helpStore.loadList(1, 20, true) }
  catch { /* silently ignore poll errors */ }
  finally { isHelpPolling = false }
}

function startHelpPolling() {
  stopHelpPolling()
  helpPollTimer = setInterval(pollHelpList, HELP_POLL_INTERVAL_MS)
}

function stopHelpPolling() {
  if (helpPollTimer) { clearInterval(helpPollTimer); helpPollTimer = null }
  isHelpPolling = false
}

/** 进行中会话入口：仅认证急救员可见；身份资料未加载时先静默加载（BackEnd.md §4.5A 主页入口） */
async function pollMineSessions(): Promise<void> {
  if (isSessionPolling || !authStore.isLoggedIn) return
  isSessionPolling = true
  try {
    if (!userStore.userInfo) {
      await userStore.loadProfile()
    }
    if (userStore.isCertified) {
      await rescueStore.loadMineSessions(true)
    }
  } catch { /* 静默轮询容错 */ }
  finally { isSessionPolling = false }
}

function startSessionPolling() {
  stopSessionPolling()
  sessionPollTimer = setInterval(pollMineSessions, RESCUE_MINE_POLL_INTERVAL_MS)
}

function stopSessionPolling() {
  if (sessionPollTimer) { clearInterval(sessionPollTimer); sessionPollTimer = null }
  isSessionPolling = false
}

const activeSessionItem = computed(() => {
  return userStore.isCertified && rescueStore.mineItems.length > 0
    ? rescueStore.mineItems[0]
    : null
})

function openActiveSession(): void {
  const item = activeSessionItem.value
  if (!item) return
  uni.navigateTo({ url: `/pages-sub/rescue/session?id=${item.helpId}` })
}

function openHelpPage(): void {
  uni.navigateTo({ url: '/pages-sub/help/request' })
}

function openCurrentHelp(): void {
  const request = helpStore.activeRequest
  if (!request) return
  uni.navigateTo({ url: `/pages-sub/help/detail?id=${request.id}` })
}

function showActiveHelpTip(): void {
  uni.showToast({ title: '已有进行中的求助', icon: 'none' })
}

function shouldShowSubscribePrompt(): boolean {
  return USE_REAL_WECHAT_FLOW
    && Boolean(WECHAT_ALERT_TEMPLATE_ID)
    && !hasPromptedSubscribeThisSession
    && authStore.isLoggedIn
}

function clearSubscribePromptTimer(): void {
  if (!subscribePromptTimer) return
  clearTimeout(subscribePromptTimer)
  subscribePromptTimer = null
}

function scheduleSubscribePrompt(): void {
  clearSubscribePromptTimer()
  if (!shouldShowSubscribePrompt()) return

  subscribePromptTimer = setTimeout(() => {
    subscribePromptTimer = null
    if (!shouldShowSubscribePrompt()) return
    hasPromptedSubscribeThisSession = true
    showSubscribePrompt.value = true
  }, SUBSCRIBE_PROMPT_DELAY_MS)
}

function closeSubscribePrompt(): void {
  showSubscribePrompt.value = false
}

async function handleEnableSubscribe(): Promise<void> {
  if (isRequestingSubscribe.value) return

  isRequestingSubscribe.value = true
  try {
    const status = await requestAlertSubscribe()
    const accepted = status === 'accept' || status === 'acceptWithAudio'
    uni.showToast({
      title: accepted ? '已开启' : '未开启',
      icon: accepted ? 'success' : 'none',
    })
    showSubscribePrompt.value = false
  } catch (error) {
    console.warn('[MapHome] 订阅授权失败', error)
    uni.showToast({ title: '未开启', icon: 'none' })
  } finally {
    isRequestingSubscribe.value = false
  }
}

function toggleNearbyPanel(): void {
  showNearbyPanel.value = !showNearbyPanel.value
}

async function handleRelocate(): Promise<void> {
  statusBarDismissed.value = false
  await relocate()
  if (hasRealLocation.value) {
    uni.showToast({ title: '已更新当前位置', icon: 'success' })
  }
}

async function handleNearest(): Promise<void> {
  uni.vibrateLong()
  await goToNearestAed()
  if (!aedStore.nearestAed) {
    uni.showToast({ title: '未找到可用 AED', icon: 'none' })
  }
}

function navigateToAed(aed: AedItem): void {
  openNativeNavigation(aed.lat, aed.lng, aed.name, aed.address)
}

function handleMarkerTap(event: MarkerTapEvent): void {
  lastMarkerTapAt = Date.now()
  const markerId = event.markerId ?? event.detail?.markerId
  if (markerId == null) return

  const aed = markerId >= RIPPLE_ID_BASE
    ? getActiveNearestAed() ?? undefined
    : aedStore.aedList[markerId]
  if (!aed) return

  focusAedById(aed.id)
  selectedAed.value = aed
  showAedModal.value = true
}

function handleModalNavigate(): void {
  const aed = selectedAed.value
  if (!aed) return

  showAedModal.value = false
  navigateToAed(aed)
}

function handlePreviewAedImage(): void {
  const imageUrl = selectedAed.value?.image
  if (!imageUrl) return

  uni.previewImage({ current: imageUrl, urls: [imageUrl] })
}

function handleMapTap(): void {
  const isMarkerFollowTap = Date.now() - lastMarkerTapAt < MARKER_TAP_GUARD_MS
  if (isMarkerFollowTap || !isNearestActive.value) {
    return
  }

  clearRoute()
}

function handleMapError(event: Event): void {
  const payload = event as unknown as { detail?: { errMsg?: string } }
  mapErrorMessage.value = payload.detail?.errMsg || MAP_LOAD_ERROR_TEXT
}
</script>

<style lang="scss" scoped>
.map-home {
  position: relative;
  width: 100%;
  height: 100vh;
}

.map-canvas {
  width: 100%;
  height: 100%;
}

.subscribe-prompt {
  padding: $spacing-xs $spacing-md $spacing-sm;
}

.subscribe-prompt__text {
  color: $text-secondary;
  font-size: $font-size-base;
  line-height: 1.6;
}

.subscribe-prompt__actions {
  display: flex;
  gap: $spacing-sm;
  width: 100%;
}

.subscribe-prompt__btn {
  flex: 1;
  height: 88rpx;
  border-radius: $radius-md;
  font-size: $font-size-base;
  line-height: 88rpx;
}

.subscribe-prompt__btn::after {
  border: 0;
}

.subscribe-prompt__btn--ghost {
  background: $background;
  color: $text-secondary;
}

.subscribe-prompt__btn--primary {
  background: $primary;
  color: $text-white;
}
</style>
