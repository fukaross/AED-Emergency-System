/**
 * AED 设备 + 地图状态 Store
 *
 * 管理 AED 列表、最近 AED、用户位置、导航路线
 * 地图状态不拆独立 store（D-03 决策）
 *
 * 来源：CLAUDE.md §6.3
 */
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { AedItem } from '@/types/aed'
import type { GeoPoint } from '@/types/common'
import { fetchNearbyAed } from '@/api/aed'
import { findNearestOnlineAed } from '@/services/geo-service'
import { AED_MOCK_DATA } from '@/mock/aed'

export const useAedStore = defineStore('aed', () => {
  // ===== State =====
  const aedList = ref<AedItem[]>([])
  const nearestAed = ref<AedItem | null>(null)
  const userLocation = ref<GeoPoint | null>(null)
  const routePolyline = ref<GeoPoint[]>([])
  const pendingFocusAedId = ref<string | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  // ===== Getters =====
  const onlineAedList = computed(() =>
    aedList.value.filter(item => item.status === 'online'),
  )

  const sortedAedList = computed(() => {
    const list = [...aedList.value]
    list.sort((a, b) => {
      if (a.status === 'online' && b.status !== 'online') return -1
      if (a.status !== 'online' && b.status === 'online') return 1
      return a.distance - b.distance
    })
    return list
  })

  // ===== Actions =====
  async function loadNearbyAed(lat: number, lng: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      aedList.value = await fetchNearbyAed({ lat, lng })
    } catch (err) {
      error.value = (err as Error).message
      // 后端未就绪时回退 mock 数据
      if (aedList.value.length === 0) {
        aedList.value = [...AED_MOCK_DATA]
      }
    } finally {
      loading.value = false
    }
  }

  function setUserLocation(point: GeoPoint): void {
    userLocation.value = point
  }

  function findNearest(): void {
    if (!userLocation.value) return
    nearestAed.value = findNearestOnlineAed(aedList.value, userLocation.value)
  }

  function setRoutePolyline(points: GeoPoint[]): void {
    routePolyline.value = points
  }

  function clearRoute(): void {
    routePolyline.value = []
    nearestAed.value = null
  }

  function setPendingFocusAed(id: string): void {
    pendingFocusAedId.value = id
  }

  function consumePendingFocusAed(): string | null {
    const id = pendingFocusAedId.value
    pendingFocusAedId.value = null
    return id
  }

  return {
    aedList,
    nearestAed,
    userLocation,
    routePolyline,
    pendingFocusAedId,
    loading,
    error,
    onlineAedList,
    sortedAedList,
    loadNearbyAed,
    setUserLocation,
    findNearest,
    setRoutePolyline,
    clearRoute,
    setPendingFocusAed,
    consumePendingFocusAed,
  }
})
