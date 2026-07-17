// 急救会话监控地图 —— 高德多 marker 动态增删/更新（只读监控，BackEnd.md §4.12）
import { ref } from 'vue'
import type { AMapModule, AmapMap, AmapMarker } from '@/types/amap'
import type { AdminRescueParticipant, RescueSos } from '@/types/rescue-session'
import { loadAmap } from '@/utils/amap-loader'
import { participantMarkerMeta, SOS_MARKER_META, type RescueMarkerMeta } from '@/utils/rescue-session'

const SOS_MARKER_KEY = '__sos__'
const DEFAULT_CENTER: [number, number] = [110.51346, 19.976547]

export function useRescueMap() {
  const loading = ref(false)
  const error = ref('')

  let AMapMod: AMapModule | null = null
  let mapInstance: AmapMap | null = null
  let fittedOnce = false
  const markers = new Map<string, AmapMarker>()

  async function initMap(containerId: string, center?: [number, number]): Promise<void> {
    loading.value = true
    error.value = ''
    try {
      AMapMod = await loadAmap()
      mapInstance = new AMapMod.Map(containerId, {
        zoom: 15,
        center: center ?? DEFAULT_CENTER,
        resizeEnable: true,
        viewMode: '2D',
      })
    } catch (err) {
      error.value = err instanceof Error ? err.message : '高德地图加载失败'
    } finally {
      loading.value = false
    }
  }

  /** 同步 SOS 点与参与者位置：新增添加、移动更新、消失移除，首个有效帧自适应视野 */
  function syncMarkers(sos: RescueSos, participants: readonly AdminRescueParticipant[]): void {
    if (!mapInstance || !AMapMod) return

    const wanted = new Map<string, { position: [number, number]; meta: RescueMarkerMeta }>()
    wanted.set(SOS_MARKER_KEY, { position: [sos.lng, sos.lat], meta: SOS_MARKER_META })
    for (const participant of participants) {
      if (!participant.location) continue
      wanted.set(participant.openId, {
        position: [participant.location.lng, participant.location.lat],
        meta: participantMarkerMeta(participant),
      })
    }

    for (const [key, marker] of markers) {
      if (!wanted.has(key)) {
        mapInstance.remove(marker)
        markers.delete(key)
      }
    }

    for (const [key, target] of wanted) {
      const existing = markers.get(key)
      if (existing) {
        existing.setPosition(new AMapMod.LngLat(target.position[0], target.position[1]))
        continue
      }
      const marker = new AMapMod.Marker({
        position: target.position,
        content: buildMarkerContent(target.meta),
      })
      mapInstance.add(marker)
      markers.set(key, marker)
    }

    if (!fittedOnce && wanted.size > 0) {
      mapInstance.setFitView()
      fittedOnce = true
    }
  }

  function buildMarkerContent(meta: RescueMarkerMeta): string {
    return `<div style="width:28px;height:28px;border-radius:50%;background:${meta.color};color:#fff;display:flex;align-items:center;justify-content:center;font-size:13px;border:2px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.3)">${meta.label}</div>`
  }

  function destroyMap(): void {
    markers.clear()
    mapInstance?.destroy()
    mapInstance = null
    fittedOnce = false
  }

  return { loading, error, initMap, syncMarkers, destroyMap }
}
