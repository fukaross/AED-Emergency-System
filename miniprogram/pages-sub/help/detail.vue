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
      :show-location="Boolean(userLocation)"
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

      <HelpRescueView v-if="rescueView" :view="rescueView" :stale="rescueViewStale" />
    </scroll-view>

    <view v-if="canShowReportAction(detail)" class="fixed-footer">
      <view class="finish-btn" @click="openReport">
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
import { MAP_CENTER } from '@/config/site-config'
import { dialPhone } from '@/utils/dial'
import { buildViewParticipantMarker } from '@/utils/rescue-markers'
import {
  canShowReportAction,
  collectRescueMapPoints,
  isActiveHelpStatus,
} from './rescue-view-state'
import {
  AED_MARKER_OFFSET,
  HELP_MARKER_ID,
  buildAedMarker,
  buildHelpMarker,
} from './help-detail-map'
import { useHelpRescueView } from './useHelpRescueView'
import { useHelpDetailPolling } from './useHelpDetailPolling'
import HelpDetailInfo from './components/help-detail-info.vue'
import HelpReportSheet from './components/help-report-sheet.vue'
import HelpRescueView from './components/help-rescue-view.vue'

interface MarkerTapEvent {
  readonly markerId?: number
  readonly detail?: { readonly markerId?: number }
}

const helpStore = useHelpStore()
const { themeClass } = useTheme()
const detail = ref<HelpRequestDetail | null>(null)
const nearbyAeds = ref<readonly AedItem[]>([])
const loading = ref(true)
const errorMessage = ref('')
const showReportSheet = ref(false)
const submittingReport = ref(false)
const userLocation = ref<GeoPoint | null>(null)
let isPageVisible = false
let autoOpenReport = false
const {
  rescueView,
  rescueViewStale,
  animatedParticipants,
  refreshRescueView,
  startRescuePolling,
  clearRescueView,
  suspendRescueView,
} = useHelpRescueView(detail)
const {
  refreshHelpDetail,
  startHelpPolling,
  stopHelpPolling,
} = useHelpDetailPolling({
  detail,
  applyDetail: applyHelpDetail,
  handleAccessEnded: handleDetailAccessEnded,
})
const mapCenter = computed(() => {
  return detail.value
    ? { lat: detail.value.lat, lng: detail.value.lng }
    : { lat: MAP_CENTER.lat, lng: MAP_CENTER.lng }
})

const markers = computed<MapMarker[]>(() => {
  if (!detail.value) return []
  const helpMarker = buildHelpMarker(detail.value)
  const aedMarkers = nearbyAeds.value.map(buildAedMarker)
  const participantMarkers = animatedParticipants.value
    .map((participant, index) => buildViewParticipantMarker(participant, index))
    .filter((marker): marker is MapMarker => marker !== null)
  return [helpMarker, ...aedMarkers, ...participantMarkers]
})

const polylines = computed<MapPolyline[]>(() => {
  if (!detail.value || !userLocation.value) return []
  const origin = userLocation.value
  return [createRoutePolyline([origin, mapCenter.value])]
})

const includePoints = computed(() => {
  return collectRescueMapPoints({
    userLocation: userLocation.value,
    sos: detail.value ? { lat: detail.value.lat, lng: detail.value.lng } : null,
    participants: animatedParticipants.value,
  })
})
onLoad((query = {}) => {
  const id = String(query.id || query.requestNo || '')
  autoOpenReport = String(query.report || '') === '1'
  void loadDetail(id)
})

async function loadDetail(id: string): Promise<void> {
  if (!id) {
    errorMessage.value = '缺少求助编号'
    loading.value = false
    return
  }

  const shouldAutoOpenReport = autoOpenReport
  autoOpenReport = false
  loading.value = true
  try {
    await helpStore.loadDetail(id)
    applyHelpDetail(helpStore.currentRequest)
    await Promise.all([loadNearbyAeds(), loadUserLocation()])
    if (isPageVisible && isActiveHelpStatus(detail.value?.status)) {
      const rescueResult = await refreshRescueView()
      startHelpPolling()
      if (rescueResult !== 'terminal') startRescuePolling()
    }
    if (shouldAutoOpenReport) {
      if (canShowReportAction(detail.value)) {
        openReport()
      } else {
        uni.showToast({ title: '当前不可提交报告', icon: 'none' })
      }
    }
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
  isPageVisible = true
  void resumePage()
})

onHide(() => { suspendPage() })

onUnload(() => { suspendPage() })

onPullDownRefresh(async () => {
  if (!detail.value?.id) {
    uni.stopPullDownRefresh()
    return
  }
  try {
    await refreshHelpDetail()
    await loadNearbyAeds()
  } catch { /* ignore */ }
  finally { uni.stopPullDownRefresh() }
})

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
  dialPhone(detail.value.requesterPhone)
}

function previewScene(index: number): void {
  if (!detail.value) return
  uni.previewImage({ current: detail.value.images[index], urls: [...detail.value.images] })
}

function openReport(): void {
  if (!canShowReportAction(detail.value)) {
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
    applyHelpDetail(helpStore.currentRequest)
    showReportSheet.value = false
    uni.showToast({ title: '已提交', icon: 'success' })
  } finally {
    submittingReport.value = false
  }
}

async function resumePage(): Promise<void> {
  const helpId = detail.value?.id
  if (!helpId) return
  try {
    await refreshHelpDetail()
    if (!isPageVisible || detail.value?.id !== helpId) return
    if (!isActiveHelpStatus(detail.value?.status)) return
    const rescueResult = await refreshRescueView()
    startHelpPolling()
    if (rescueResult !== 'terminal') startRescuePolling()
  } catch { /* 静默恢复，保留已加载内容 */ }
}

function suspendPage(): void {
  isPageVisible = false
  stopHelpPolling()
  suspendRescueView()
}

function applyHelpDetail(next: HelpRequestDetail | null): void {
  detail.value = next
  if (!isActiveHelpStatus(next?.status)) handleTerminalHelpState()
}

function handleTerminalHelpState(): void {
  clearRescueView()
  stopHelpPolling()
}

function handleDetailAccessEnded(): void {
  detail.value = null
  errorMessage.value = '无法继续查看该求助'
  handleTerminalHelpState()
}
</script>
<style lang="scss" scoped src="./detail.scss" />
