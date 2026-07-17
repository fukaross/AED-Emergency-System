import { ref } from 'vue'
import type { AMapModule, AmapMap, AmapMarker } from '@/types/amap'
import type { AdminRescueParticipant, RescueSos } from '@/types/rescue-session'
import { loadAmap } from '@/utils/amap-loader'
import { participantMarkerMeta, SOS_MARKER_META, type RescueMarkerMeta } from '@/utils/rescue-session'

const SOS_MARKER_KEY = '__sos__'
const DEFAULT_CENTER: [number, number] = [110.51346, 19.976547]
const DEFAULT_ZOOM = 15

interface MarkerTarget {
  readonly position: [number, number]
  readonly meta: RescueMarkerMeta
}

interface RescueMapOptions {
  readonly loadMap?: () => Promise<AMapModule>
}

class RescueMapController {
  readonly loading = ref(false)
  readonly error = ref('')

  private AMapMod: AMapModule | null = null
  private mapInstance: AmapMap | null = null
  private fittedParticipantsOnce = false
  private initGeneration = 0
  private readonly markers = new Map<string, AmapMarker>()

  constructor(private readonly loadMap: () => Promise<AMapModule>) {}

  async initMap(containerId: string, center?: [number, number]): Promise<void> {
    const generation = ++this.initGeneration
    this.loading.value = true
    this.error.value = ''
    try {
      const loadedModule = await this.loadMap()
      if (generation !== this.initGeneration) return
      this.AMapMod = loadedModule
      this.mapInstance = new loadedModule.Map(containerId, {
        zoom: DEFAULT_ZOOM,
        center: center ?? DEFAULT_CENTER,
        resizeEnable: true,
        viewMode: '2D',
      })
    } catch (error: unknown) {
      if (generation !== this.initGeneration) return
      this.error.value = error instanceof Error ? error.message : '高德地图加载失败'
    } finally {
      if (generation === this.initGeneration) this.loading.value = false
    }
  }

  /** 每个快照同时同步位置和视觉状态，避免 marker 状态滞后。 */
  syncMarkers(sos: RescueSos, participants: readonly AdminRescueParticipant[]): void {
    if (!this.mapInstance || !this.AMapMod) return
    const targets = this.createTargets(sos, participants)
    this.removeStaleMarkers(targets)
    this.upsertMarkers(targets)
    this.fitFirstParticipants(targets)
  }

  destroyMap(): void {
    this.initGeneration += 1
    this.loading.value = false
    this.markers.clear()
    this.mapInstance?.destroy()
    this.mapInstance = null
    this.AMapMod = null
    this.fittedParticipantsOnce = false
  }

  private createTargets(
    sos: RescueSos,
    participants: readonly AdminRescueParticipant[],
  ): Map<string, MarkerTarget> {
    const targets = new Map<string, MarkerTarget>()
    targets.set(SOS_MARKER_KEY, { position: [sos.lng, sos.lat], meta: SOS_MARKER_META })
    for (const participant of participants) {
      if (!participant.location || participant.signal === 'gone') continue
      targets.set(participant.openId, {
        position: [participant.location.lng, participant.location.lat],
        meta: participantMarkerMeta(participant),
      })
    }
    return targets
  }

  private removeStaleMarkers(targets: ReadonlyMap<string, MarkerTarget>): void {
    if (!this.mapInstance) return
    for (const [key, marker] of this.markers) {
      if (targets.has(key)) continue
      this.mapInstance.remove(marker)
      this.markers.delete(key)
    }
  }

  private upsertMarkers(targets: ReadonlyMap<string, MarkerTarget>): void {
    if (!this.mapInstance || !this.AMapMod) return
    for (const [key, target] of targets) {
      const existing = this.markers.get(key)
      const content = buildMarkerContent(target.meta)
      if (existing) {
        existing.setPosition(new this.AMapMod.LngLat(target.position[0], target.position[1]))
        existing.setContent(content)
        continue
      }
      const marker = new this.AMapMod.Marker({ position: target.position, content })
      this.mapInstance.add(marker)
      this.markers.set(key, marker)
    }
  }

  private fitFirstParticipants(targets: ReadonlyMap<string, MarkerTarget>): void {
    if (this.fittedParticipantsOnce || targets.size <= 1) return
    this.mapInstance?.setFitView()
    this.fittedParticipantsOnce = true
  }
}

function buildMarkerContent(meta: RescueMarkerMeta): string {
  return `<div style="width:28px;height:28px;border-radius:50%;background:${meta.color};color:#fff;display:flex;align-items:center;justify-content:center;font-size:13px;border:2px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.3)">${meta.label}</div>`
}

export function useRescueMap(options: RescueMapOptions = {}) {
  const controller = new RescueMapController(options.loadMap ?? loadAmap)
  return {
    loading: controller.loading,
    error: controller.error,
    initMap: controller.initMap.bind(controller),
    syncMarkers: controller.syncMarkers.bind(controller),
    destroyMap: controller.destroyMap.bind(controller),
  }
}
