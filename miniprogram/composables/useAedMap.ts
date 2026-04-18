/**
 * 地图页交互编排 composable
 *
 * 连接 AED Store、地图服务、定位服务
 * 编排"加载 AED → 定位 → 找最近 → 路线规划"流程
 *
 * D-05 基线要求
 */
import { ref, onMounted, onUnmounted } from 'vue'
import { useAedStore } from '@/store/aed'
import type { AedItem } from '@/types/aed'
import { getCurrentLocation, shouldSkipInitialRealLocation } from '@/services/geo-service'
import {
  aedListToMarkers,
  createActiveMarker,
  createBreathingMarker,
  createRoutePolyline,
  fetchWalkingRoute,
  type MapMarker,
  type MapPolyline,
} from '@/services/map-service'
import { MAP_CENTER, DEFAULT_MAP_ZOOM } from '@/utils/constants'
import {
  APP_ENV,
  AUTO_LOCATE_ON_INIT,
  USE_MOCK,
  USE_REAL_LOCATION,
} from '@/utils/runtime-env'

const INITIAL_LOCATION_TIP =
  '预览模式首次进入不自动定位，点击“重新定位”手动选择调试位置'

export function useAedMap() {
  const aedStore = useAedStore()

  const mapCenter = ref<{ latitude: number; longitude: number }>({
    latitude: MAP_CENTER.latitude,
    longitude: MAP_CENTER.longitude,
  })
  const mapZoom = ref(DEFAULT_MAP_ZOOM)
  const markers = ref<MapMarker[]>([])
  const polylines = ref<MapPolyline[]>([])
  const locationError = ref<string | null>(null)
  const isLocating = ref(false)
  const hasRealLocation = ref(false)

  // ===== 呼吸动画（最近 AED 标注，CLAUDE.md §7.1） =====
  const BREATHE_SIZES = [60, 66, 60, 54] as const
  const BREATHE_STEP_MS = 500
  let breatheTimer: ReturnType<typeof setInterval> | null = null
  let breatheStep = 0

  function startBreathAnimation(
    nearest: AedItem,
    markerId: number,
    baseMarkers: MapMarker[],
  ): void {
    stopBreathAnimation()
    breatheStep = 0
    breatheTimer = setInterval(() => {
      breatheStep = (breatheStep + 1) % BREATHE_SIZES.length
      const size = BREATHE_SIZES[breatheStep]
      const breathingMarker = createBreathingMarker(nearest, markerId, size)
      markers.value = [...baseMarkers, breathingMarker]
    }, BREATHE_STEP_MS)
  }

  function stopBreathAnimation(): void {
    if (breatheTimer !== null) {
      clearInterval(breatheTimer)
      breatheTimer = null
    }
  }

  async function refreshAed(point: { lat: number; lng: number }): Promise<void> {
    aedStore.setUserLocation(point)
    mapCenter.value = { latitude: point.lat, longitude: point.lng }
    await aedStore.loadNearbyAed(point.lat, point.lng)
    markers.value = aedListToMarkers(aedStore.aedList)
  }

  async function useInitialFallbackLocation(): Promise<void> {
    hasRealLocation.value = false
    locationError.value = INITIAL_LOCATION_TIP
    await refreshAed({ lat: MAP_CENTER.latitude, lng: MAP_CENTER.longitude })
  }

  /** 初始化：定位 + 加载 AED */
  async function init(): Promise<void> {
    if (
      shouldSkipInitialRealLocation(
        APP_ENV,
        USE_MOCK,
        USE_REAL_LOCATION,
        AUTO_LOCATE_ON_INIT,
      )
    ) {
      await useInitialFallbackLocation()
      return
    }

    isLocating.value = true
    const loc = await getCurrentLocation({ mode: 'auto' })
    isLocating.value = false
    hasRealLocation.value = loc.showSystemLocation

    if (loc.isDefault && loc.errorMessage) {
      locationError.value = loc.errorMessage
    } else {
      locationError.value = null
    }
    await refreshAed(loc.point)
  }

  /** 回到我的位置 */
  async function relocate(): Promise<void> {
    isLocating.value = true
    const loc = await getCurrentLocation({ mode: 'interactive' })
    isLocating.value = false
    hasRealLocation.value = loc.showSystemLocation

    if (loc.success) {
      locationError.value = null
      await refreshAed(loc.point)
      return
    }

    if (loc.errorMessage) {
      locationError.value = loc.errorMessage
    }
  }

  /** 找最近 AED + 路线规划 + 呼吸动画 */
  async function goToNearestAed(): Promise<void> {
    stopBreathAnimation()
    aedStore.findNearest()
    const nearest = aedStore.nearestAed
    if (!nearest || !aedStore.userLocation) return

    // 地图聚焦
    mapCenter.value = { latitude: nearest.lat, longitude: nearest.lng }

    // 移除原始 marker，替换为高亮态（避免两个 marker 叠加导致错位）
    const nearestIdx = aedStore.aedList.findIndex(a => a.id === nearest.id)
    const markerId = nearestIdx >= 0 ? nearestIdx : 999
    const baseMarkers = aedListToMarkers(aedStore.aedList)
      .filter((_, i) => i !== nearestIdx)
    const activeMarker = createActiveMarker(nearest, markerId)
    markers.value = [...baseMarkers, activeMarker]

    // 启动呼吸动画（尺寸 54↔66 循环，周期 2s）
    startBreathAnimation(nearest, markerId, baseMarkers)

    // 路线规划
    const from = aedStore.userLocation
    const to = { lat: nearest.lat, lng: nearest.lng }
    const routePoints = await fetchWalkingRoute(from, to)

    if (routePoints.length > 0) {
      polylines.value = [createRoutePolyline(routePoints)]
      aedStore.setRoutePolyline(routePoints)
    }
  }

  /** 清除路线与高亮 */
  function clearRoute(): void {
    stopBreathAnimation()
    polylines.value = []
    markers.value = aedListToMarkers(aedStore.aedList)
    aedStore.clearRoute()
  }

  onMounted(init)
  onUnmounted(stopBreathAnimation)

  return {
    mapCenter,
    mapZoom,
    markers,
    polylines,
    locationError,
    isLocating,
    hasRealLocation,
    init,
    relocate,
    goToNearestAed,
    clearRoute,
  }
}
