// 地图页交互编排 —— 加载 AED → 定位 → 找最近 → 路线规划
import { ref, computed, nextTick, onMounted, onUnmounted } from 'vue'
import { onShow, onHide } from '@dcloudio/uni-app'
import { useAedStore } from '@/store/aed'
import type { AedItem } from '@/types/aed'
import { getCurrentLocation, shouldSkipInitialRealLocation } from '@/services/geo-service'
import {
  aedListToMarkers,
  createActiveMarker,
  createBreathingMarker,
  createRippleMarker,
  createRoutePolyline,
  fetchWalkingRoute,
  getBreathSize,
  getRippleSize,
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
  '预览模式首次进入不自动定位，点击“重新定位”获取当前位置'

export function useAedMap() {
  const aedStore = useAedStore()

  const mapCenter = ref<{ latitude: number; longitude: number }>({
    latitude: MAP_CENTER.latitude,
    longitude: MAP_CENTER.longitude,
  })
  const mapZoom = ref(DEFAULT_MAP_ZOOM)
  const baseMarkers = ref<MapMarker[]>([])
  const animMarkers = ref<MapMarker[]>([])
  const markers = computed<MapMarker[]>(() =>
    [...baseMarkers.value, ...animMarkers.value],
  )
  const polylines = ref<MapPolyline[]>([])
  const locationError = ref<string | null>(null)
  const isLocating = ref(false)
  const hasRealLocation = ref(false)
  const isNearestActive = computed(
    () => animMarkers.value.length > 0 || polylines.value.length > 0,
  )

  // ===== 动画（呼吸 + 涟漪）=====
  const ANIM_INTERVAL_MS = 200
  const ANIM_CYCLE_STEPS = 10
  const RIPPLE_RING_COUNT = 2
  let animTimer: ReturnType<typeof setInterval> | null = null
  let animStep = 0
  let activeNearestAed: AedItem | null = null
  let activeMarkerId = -1

  function buildNearestAnimationMarkers(
    nearest: AedItem,
    markerId: number,
    step: number,
  ): MapMarker[] {
    const breathSize = getBreathSize(step, ANIM_CYCLE_STEPS)
    const breathMarker = createBreathingMarker(nearest, markerId, breathSize)
    const ripples: MapMarker[] = []

    for (let i = 0; i < RIPPLE_RING_COUNT; i++) {
      const size = getRippleSize(step, ANIM_CYCLE_STEPS, i, RIPPLE_RING_COUNT)
      ripples.push(createRippleMarker(nearest.lat, nearest.lng, i, size))
    }

    return [...ripples, breathMarker]
  }

  function startNearestAnimation(
    nearest: AedItem,
    markerId: number,
  ): void {
    stopNearestAnimation()
    activeNearestAed = nearest
    activeMarkerId = markerId
    animStep = 0
    animMarkers.value = buildNearestAnimationMarkers(nearest, markerId, animStep)
    animTimer = setInterval(() => {
      animStep++
      if (!activeNearestAed) return
      animMarkers.value = buildNearestAnimationMarkers(
        activeNearestAed, activeMarkerId, animStep,
      )
    }, ANIM_INTERVAL_MS)
  }

  function stopNearestAnimation(): void {
    if (animTimer !== null) {
      clearInterval(animTimer)
      animTimer = null
    }
    activeNearestAed = null
    activeMarkerId = -1
  }

  /** 获取当前动画对应的 AED（供 marker 点击判定） */
  function getActiveNearestAed(): AedItem | null {
    return activeNearestAed
  }

  function syncActiveNearestAfterRefresh(): void {
    if (!activeNearestAed) return
    const latest = aedStore.aedList.find(item => item.id === activeNearestAed?.id)
    if (!latest || latest.status !== 'online') {
      stopNearestAnimation()
      animMarkers.value = []
      polylines.value = []
      aedStore.clearRoute()
      return
    }
    activeNearestAed = latest
    activeMarkerId = aedStore.aedList.findIndex(item => item.id === latest.id)
  }

  function rebuildBaseMarkers(): void {
    const nextMarkers = aedListToMarkers(aedStore.aedList)
    if (!activeNearestAed) {
      baseMarkers.value = nextMarkers
      return
    }
    const activeIndex = aedStore.aedList.findIndex(item => item.id === activeNearestAed?.id)
    baseMarkers.value = activeIndex >= 0
      ? nextMarkers.filter((_, index) => index !== activeIndex)
      : nextMarkers
  }

  async function refreshAed(point: { lat: number; lng: number }): Promise<void> {
    aedStore.setUserLocation(point)
    mapCenter.value = { latitude: point.lat, longitude: point.lng }
    await aedStore.loadNearbyAed(point.lat, point.lng)
    syncActiveNearestAfterRefresh()
    rebuildBaseMarkers()
    if (!activeNearestAed) {
      animMarkers.value = []
    }
  }

  async function refreshCurrentAed(): Promise<void> {
    const point = aedStore.userLocation
      ?? { lat: mapCenter.value.latitude, lng: mapCenter.value.longitude }
    await refreshAed(point)
    applyPendingFocus()
  }

  const FOCUS_ZOOM = DEFAULT_MAP_ZOOM + 2

  function focusAedById(id: string): boolean {
    const target = aedStore.aedList.find((item) => item.id === id)
    if (!target) {
      return false
    }

    clearRoute()

    const FOCUS_DELAY_MS = 150
    // 先设微偏移 + 缩小一级，强制 Vue 检测到变化
    mapCenter.value = {
      latitude: target.lat + 0.000001,
      longitude: target.lng + 0.000001,
    }
    mapZoom.value = FOCUS_ZOOM - 1

    // 第一阶段：平移到精确目标
    nextTick(() => {
      mapCenter.value = { latitude: target.lat, longitude: target.lng }
    })
    // 第二阶段：延迟后放大
    setTimeout(() => {
      mapZoom.value = FOCUS_ZOOM
    }, FOCUS_DELAY_MS)

    return true
  }

  function applyPendingFocus(): void {
    const pendingId = aedStore.pendingFocusAedId
    if (!pendingId) {
      return
    }

    if (focusAedById(pendingId)) {
      aedStore.consumePendingFocusAed()
    }
  }

  async function useInitialFallbackLocation(): Promise<void> {
    hasRealLocation.value = false
    locationError.value = INITIAL_LOCATION_TIP
    await refreshAed({ lat: MAP_CENTER.latitude, lng: MAP_CENTER.longitude })
    applyPendingFocus()
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
    applyPendingFocus()
  }

  /** 回到我的位置 */
  async function relocate(): Promise<void> {
    isLocating.value = true
    const loc = await getCurrentLocation({ mode: 'auto' })
    isLocating.value = false
    hasRealLocation.value = loc.showSystemLocation

    if (loc.success) {
      locationError.value = null
      await refreshAed(loc.point)
      applyPendingFocus()
      return
    }

    if (loc.errorMessage) {
      locationError.value = loc.errorMessage
    }
  }

  /** 找最近 AED + 路线规划 + 呼吸动画 */
  async function goToNearestAed(): Promise<void> {
    stopNearestAnimation()
    aedStore.findNearest()
    const nearest = aedStore.nearestAed
    if (!nearest || !aedStore.userLocation) return

    // 地图聚焦 — 两阶段平滑：先平移+缩小一级，延迟后再放大
    const FOCUS_DELAY_MS = 150
    mapCenter.value = {
      latitude: nearest.lat + 0.000001,
      longitude: nearest.lng + 0.000001,
    }
    mapZoom.value = FOCUS_ZOOM - 1
    // 第一阶段：平移到精确目标
    nextTick(() => {
      mapCenter.value = { latitude: nearest.lat, longitude: nearest.lng }
    })
    // 第二阶段：延迟后放大，形成"平移 → 放大"的视觉节奏
    setTimeout(() => {
      mapZoom.value = FOCUS_ZOOM
    }, FOCUS_DELAY_MS)

    // 移除原始 marker，替换为高亮态（避免两个 marker 叠加导致错位）
    const nearestIdx = aedStore.aedList.findIndex(a => a.id === nearest.id)
    const markerId = nearestIdx >= 0 ? nearestIdx : 999
    baseMarkers.value = aedListToMarkers(aedStore.aedList)
      .filter((_, i) => i !== nearestIdx)
    animMarkers.value = [createActiveMarker(nearest, markerId)]

    // 启动呼吸 + 涟漪动画（正弦波 2s 周期 + 3 环扩散）
    startNearestAnimation(nearest, markerId)

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
    stopNearestAnimation()
    animMarkers.value = []
    polylines.value = []
    // 下一帧恢复基础 markers，避免与动画清理冲突
    nextTick(() => {
      baseMarkers.value = aedListToMarkers(aedStore.aedList)
    })
    aedStore.clearRoute()
  }

  // ===== 页面生命周期 =====

  onMounted(init)

  onShow(() => {
    applyPendingFocus()
    // 从其他 Tab 返回时，如果动画之前在播放，重新启动
    if (activeNearestAed && activeMarkerId >= 0 && animTimer === null) {
      startNearestAnimation(activeNearestAed, activeMarkerId)
    }
  })

  onHide(() => {
    // Tab 切走时暂停动画并清理视觉 marker，防止 native map 状态残留
    if (animTimer !== null) {
      clearInterval(animTimer)
      animTimer = null
      animMarkers.value = []
    }
  })

  onUnmounted(() => {
    stopNearestAnimation()
    animMarkers.value = []
  })

  return {
    mapCenter,
    mapZoom,
    markers,
    polylines,
    locationError,
    isLocating,
    hasRealLocation,
    isNearestActive,
    init,
    refreshCurrentAed,
    focusAedById,
    relocate,
    goToNearestAed,
    clearRoute,
    getActiveNearestAed,
  }
}
