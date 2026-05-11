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
      @open-help="openHelpPage"
      @toggle-nearby="toggleNearbyPanel"
      @relocate="handleRelocate"
      @nearest="handleNearest"
    />

    <VitalSignsCard
      :heart-rate="vitalsStore.currentHeartRate"
      :latest-blood-pressure="vitalsStore.latestBloodPressure"
      :heart-rate-status="vitalsStore.heartRateStatus"
      :blood-pressure-stale="vitalsStore.bloodPressureStale"
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
import { useVitalsStore } from '@/store/vitals'
import type { AedItem } from '@/types/aed'
import { MAP_CENTER } from '@/utils/constants'
import AedDetailModal from './components/aed-detail-modal.vue'
import MapFloatingActions from './components/map-floating-actions.vue'
import MapStatusOverlay from './components/map-status-overlay.vue'
import NearbyAedPanel from './components/nearby-aed-panel.vue'
import VitalSignsCard from './components/vital-signs-card.vue'

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
const vitalsStore = useVitalsStore()
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
const selectedAed = ref<AedItem | null>(null)

let lastMarkerTapAt = 0

onMounted(() => {
  createMapCtx(MAP_ID)
})

onShow(() => {
  const loc = aedStore.userLocation
  aedStore.loadNearbyAed(
    loc?.lat ?? MAP_CENTER.latitude,
    loc?.lng ?? MAP_CENTER.longitude,
  )
  vitalsStore.startMonitoring()
})

onHide(() => {
  vitalsStore.stopMonitoring()
})

onUnmounted(() => {
  vitalsStore.stopMonitoring()
})

const statusMessage = computed(() => {
  if (mapErrorMessage.value) return mapErrorMessage.value
  if (locationError.value) return locationError.value
  if (aedStore.error) return aedStore.error
  return ''
})

function openHelpPage(): void {
  uni.navigateTo({ url: '/pages-sub/help/request' })
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
</style>
