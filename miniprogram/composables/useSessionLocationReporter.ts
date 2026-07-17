// 会话页前台位置上报：每 5s 取一次定位并上报（BackEnd.md §4.5A 位置采集约定）
import { onUnmounted } from 'vue'
import { getCurrentLocation } from '@/services/geo-service'
import type { GeoPoint } from '@/types/common'
import { RESCUE_LOCATION_REPORT_INTERVAL_MS } from '@/utils/rescue-signal'

const LOCATION_TIMEOUT_MS = 3000

export function useSessionLocationReporter(report: (point: GeoPoint) => Promise<void>) {
  let timer: ReturnType<typeof setInterval> | null = null
  let reporting = false

  async function tick(): Promise<void> {
    if (reporting) return
    reporting = true
    try {
      const location = await getCurrentLocation({ timeoutMs: LOCATION_TIMEOUT_MS })
      if (location.success) {
        await report(location.point)
      }
    } catch {
      // 上报失败不阻塞下一次节拍（轮询容错惯例，同 detail.vue refreshDetail）
    } finally {
      reporting = false
    }
  }

  function start(): void {
    stop()
    void tick()
    timer = setInterval(() => void tick(), RESCUE_LOCATION_REPORT_INTERVAL_MS)
  }

  function stop(): void {
    if (timer) {
      clearInterval(timer)
      timer = null
    }
    reporting = false
  }

  onUnmounted(stop)

  return { start, stop }
}
