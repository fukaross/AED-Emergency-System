<template>
  <view class="help-detail" :class="themeClass">
    <map
      class="detail-map"
      :latitude="mapCenter.lat"
      :longitude="mapCenter.lng"
      :scale="17"
      :markers="markers"
      :polyline="polylines"
      :include-points="includePoints"
      :show-location="hasUserLocation"
      @markertap="handleMarkerTap"
    />

    <scroll-view class="detail-panel" scroll-y>
      <view v-if="loading" class="state-line">
        <text class="state-text">加载中...</text>
      </view>

      <view v-else-if="errorMessage" class="state-line">
        <text class="state-text">{{ errorMessage }}</text>
      </view>

      <HelpDetailInfo
        v-else-if="detail"
        :detail="detail"
        @call-requester="callRequester"
        @preview-scene="previewScene"
      />
    </scroll-view>

    <view v-if="detail" class="fixed-footer">
      <view
        class="finish-btn"
        :class="{ 'is-disabled': !canSubmitReport }"
        @click="openReport"
      >
        <text class="finish-text">本次援助已完成</text>
      </view>
    </view>

    <HelpReportSheet
      v-model="showReportSheet"
      :submitting="submittingReport"
      @submit="submitReport"
    />
  </view>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { onHide, onLoad, onPullDownRefresh, onShow, onUnload } from '@dcloudio/uni-app'
import { fetchNearbyAed } from '@/api/aed'
import { useTheme } from '@/composables/useTheme'
import {
  createRoutePolyline,
  openNativeNavigation,
  type MapMarker,
  type MapPolyline,
} from '@/services/map-service'
import { getCurrentLocation } from '@/services/geo-service'
import { useHelpStore } from '@/store/help'
import type { AedItem } from '@/types/aed'
import type { GeoPoint } from '@/types/common'
import type { HelpRequestDetail, SubmitHelpReportPayload } from '@/types/help'
import { MAP_CENTER } from '@/utils/constants'
import HelpDetailInfo from './components/help-detail-info.vue'
import HelpReportSheet from './components/help-report-sheet.vue'

interface MarkerTapEvent {
  readonly markerId?: number
  readonly detail?: { readonly markerId?: number }
}

const HELP_MARKER_ID = 9000
const AED_MARKER_OFFSET = 100
const POLL_INTERVAL_MS = 10_000

const helpStore = useHelpStore()
const { themeClass } = useTheme()

const detail = ref<HelpRequestDetail | null>(null)
const nearbyAeds = ref<readonly AedItem[]>([])
const loading = ref(true)
const errorMessage = ref('')
const showReportSheet = ref(false)
const submittingReport = ref(false)
const userLocation = ref<GeoPoint | null>(null)

let pollTimer: ReturnType<typeof setInterval> | null = null
let isPolling = false

const mapCenter = computed(() => {
  return detail.value
    ? { lat: detail.value.lat, lng: detail.value.lng }
    : { lat: MAP_CENTER.latitude, lng: MAP_CENTER.longitude }
})

const markers = computed<MapMarker[]>(() => {
  if (!detail.value) return []
  const helpMarker = buildHelpMarker(detail.value)
  return [helpMarker, ...nearbyAeds.value.map(buildAedMarker)]
})

const polylines = computed<MapPolyline[]>(() => {
  if (!detail.value || !userLocation.value) return []
  const origin = userLocation.value
  return [createRoutePolyline([origin, mapCenter.value])]
})

const includePoints = computed(() => {
  const points: Array<{ latitude: number; longitude: number }> = []
  if (userLocation.value) {
    points.push({
      latitude: userLocation.value.lat,
      longitude: userLocation.value.lng,
    })
  }
  if (detail.value) {
    points.push({
      latitude: detail.value.lat,
      longitude: detail.value.lng,
    })
  }
  return points
})

const hasUserLocation = computed(() => Boolean(userLocation.value))

const canSubmitReport = computed(() => {
  return detail.value?.status === 'published' || detail.value?.status === 'responding'
})

async function refreshDetail(): Promise<void> {
  if (isPolling || !detail.value?.id) return
  if (detail.value.status === 'closed' || detail.value.status === 'false_alarm') {
    stopPolling()
    return
  }
  isPolling = true
  try {
    await helpStore.loadDetail(detail.value.id, true)
    detail.value = helpStore.currentRequest
    if (detail.value?.status === 'closed' || detail.value?.status === 'false_alarm') {
      stopPolling()
    }
  } catch { /* silently ignore poll errors */ }
  finally { isPolling = false }
}

function startPolling() {
  stopPolling()
  pollTimer = setInterval(refreshDetail, POLL_INTERVAL_MS)
}

function stopPolling() {
  if (pollTimer) { clearInterval(pollTimer); pollTimer = null }
  isPolling = false
}

onLoad((query = {}) => {
  const id = String(query.id || query.requestNo || '')
  void loadDetail(id)
})

async function loadDetail(id: string): Promise<void> {
  if (!id) {
    errorMessage.value = '缺少求助编号'
    loading.value = false
    return
  }

  loading.value = true
  try {
    await helpStore.loadDetail(id)
    detail.value = helpStore.currentRequest
    await Promise.all([loadNearbyAeds(), loadUserLocation()])
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '求助加载失败'
  } finally {
    loading.value = false
  }
}

async function loadNearbyAeds(): Promise<void> {
  if (!detail.value) return
  nearbyAeds.value = await fetchNearbyAed({
    lat: detail.value.lat,
    lng: detail.value.lng,
    radius: 1000,
  })
}

async function loadUserLocation(): Promise<void> {
  const loc = await getCurrentLocation()
  if (loc.success) {
    userLocation.value = loc.point
    return
  }

  userLocation.value = null
  uni.showToast({ title: loc.errorMessage || '定位失败', icon: 'none' })
}

onShow(() => {
  if (detail.value?.id) {
    helpStore.loadDetail(detail.value.id).then(() => {
      detail.value = helpStore.currentRequest
    }).catch(() => {})
  }
  startPolling()
})

onHide(() => { stopPolling() })

onUnload(() => { stopPolling() })

onPullDownRefresh(async () => {
  if (!detail.value?.id) {
    uni.stopPullDownRefresh()
    return
  }
  try {
    await helpStore.loadDetail(detail.value.id)
    detail.value = helpStore.currentRequest
    await loadNearbyAeds()
  } catch { /* ignore */ }
  finally { uni.stopPullDownRefresh() }
})

function buildHelpMarker(item: HelpRequestDetail): MapMarker {
  return {
    id: HELP_MARKER_ID,
    latitude: item.lat,
    longitude: item.lng,
    iconPath: '/static/images/emergency-call.png',
    width: 42,
    height: 42,
    anchor: { x: 0.5, y: 0.5 },
  }
}

function buildAedMarker(aed: AedItem, index: number): MapMarker {
  return {
    id: AED_MARKER_OFFSET + index,
    latitude: aed.lat,
    longitude: aed.lng,
    iconPath: aed.status === 'online'
      ? '/static/images/aed-marker-online.png'
      : '/static/images/aed-marker-offline.png',
    width: 36,
    height: 36,
    anchor: { x: 0.5, y: 0.5 },
  }
}

function handleMarkerTap(event: MarkerTapEvent): void {
  const markerId = event.markerId ?? event.detail?.markerId
  if (markerId === HELP_MARKER_ID) {
    navigateToRequester()
    return
  }
  const aed = nearbyAeds.value[Number(markerId) - AED_MARKER_OFFSET]
  if (aed) openNativeNavigation(aed.lat, aed.lng, aed.name, aed.address)
}

function navigateToRequester(): void {
  if (!detail.value) return
  openNativeNavigation(
    detail.value.lat,
    detail.value.lng,
    '求助位置',
    detail.value.address,
  )
}

function callRequester(): void {
  if (!detail.value?.requesterPhone) return
  uni.makePhoneCall({ phoneNumber: detail.value.requesterPhone })
}

function previewScene(index: number): void {
  if (!detail.value) return
  uni.previewImage({ current: detail.value.images[index], urls: [...detail.value.images] })
}

function openReport(): void {
  if (!canSubmitReport.value) {
    uni.showToast({ title: '当前不可提交', icon: 'none' })
    return
  }
  showReportSheet.value = true
}

async function submitReport(payload: SubmitHelpReportPayload): Promise<void> {
  if (!detail.value || submittingReport.value) return
  submittingReport.value = true
  try {
    await helpStore.submitReport(detail.value.id, payload)
    detail.value = helpStore.currentRequest
    showReportSheet.value = false
    uni.showToast({ title: '已提交', icon: 'success' })
  } finally {
    submittingReport.value = false
  }
}

</script>

<style lang="scss" scoped>
.help-detail {
  min-height: 100vh;
  background: $background;
}

.detail-map {
  width: 100%;
  height: 48vh;
}

.detail-panel {
  height: calc(52vh - 128rpx - env(safe-area-inset-bottom));
  background: $background-white;
}

.state-line {
  padding: $spacing-xl $spacing-md;
  text-align: center;
}

.state-text {
  color: $text-secondary;
  font-size: $font-size-base;
}

.fixed-footer {
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  padding: $spacing-md;
  padding-bottom: calc(#{$spacing-md} + env(safe-area-inset-bottom));
  background: $background-white;
}

.finish-btn {
  @include btn-primary;
}

.finish-btn.is-disabled {
  background-color: $background-card-disabled;
  box-shadow: none;
  pointer-events: none;
}

.finish-btn.is-disabled .finish-text {
  color: $text-secondary;
}

.finish-text {
  color: $text-white;
  font-size: $font-size-lg;
  font-weight: $font-weight-bold;
}
</style>
