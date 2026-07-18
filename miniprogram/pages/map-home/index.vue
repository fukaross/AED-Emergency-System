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
      :items="visibleSessionItems"
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

    <SubscribePromptModal
      :active="pageVisible"
      :logged-in="authStore.isLoggedIn"
    />

    <AppTabBar current-path="/pages/map-home/index" />
  </view>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { onHide, onShow } from '@dcloudio/uni-app'
import AppTabBar from '@/components/app-tab-bar/index.vue'
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
import { getVisibleRescueSessions } from './rescue-session-entry'
import { createMapHomePolling } from './useMapHomePolling'
import AedDetailModal from './components/aed-detail-modal.vue'
import CurrentHelpCard from './components/current-help-card.vue'
import MapFloatingActions from './components/map-floating-actions.vue'
import MapStatusOverlay from './components/map-status-overlay.vue'
import NearbyAedPanel from './components/nearby-aed-panel.vue'
import RescueSessionCard from './components/rescue-session-card.vue'
import SubscribePromptModal from './components/subscribe-prompt-modal.vue'

interface MarkerTapEvent {
  readonly markerId?: number
  readonly detail?: {
    readonly markerId?: number
  }
}

const MAP_ID = 'aedMap'
const MARKER_TAP_GUARD_MS = 250
const MAP_LOAD_ERROR_TEXT = '地图加载失败，请检查网络后重试'

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
const pageVisible = ref(false)
const selectedAed = ref<AedItem | null>(null)

let lastMarkerTapAt = 0

const homePolling = createMapHomePolling({
  refreshAed: refreshCurrentAed,
  loadHelp: (silent) => helpStore.loadList(1, 20, silent),
  isLoggedIn: () => authStore.isLoggedIn,
  refreshProfile: () => userStore.loadProfile(),
  isCertified: () => userStore.isCertified,
  loadSessions: () => rescueStore.loadMineSessions(userStore.userInfo?.id ?? '', true),
  resetSessions: () => rescueStore.resetMineSessions(),
})

onMounted(() => {
  createMapCtx(MAP_ID)
})

onShow(() => {
  pageVisible.value = true
  homePolling.start()
})

onHide(() => {
  pageVisible.value = false
  homePolling.stop()
})

onUnmounted(() => {
  pageVisible.value = false
  homePolling.stop()
})

const statusMessage = computed(() => {
  if (mapErrorMessage.value) return mapErrorMessage.value
  if (locationError.value) return locationError.value
  if (aedStore.error) return aedStore.error
  return ''
})

const visibleSessionItems = computed(() => getVisibleRescueSessions(
  authStore.isLoggedIn,
  userStore.isCertified,
  rescueStore.mineItems,
))

function openActiveSession(helpId: string): void {
  uni.navigateTo({ url: `/pages-sub/rescue/session?id=${helpId}` })
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

<style lang="scss" scoped src="./index.scss" />
