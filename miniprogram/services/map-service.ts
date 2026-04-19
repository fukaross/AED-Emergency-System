/**
 * 地图显示与导航编排服务
 *
 * 职责：
 * - 地图上下文管理（初始化、marker 管理、polyline 绘制）
 * - 导航路线规划（腾讯地图 WebService API）
 * - 地图中心移动、缩放控制
 *
 * 详细设计见 CLAUDE.md §4.1
 */
import type { GeoPoint } from '@/types/common'
import type { AedItem } from '@/types/aed'

/** Marker 数据结构（小程序 map 组件格式） */
export interface MapMarker {
  readonly id: number
  readonly latitude: number
  readonly longitude: number
  readonly iconPath: string
  readonly width: number
  readonly height: number
  readonly anchor?: { readonly x: number; readonly y: number }
}

/** Polyline 数据结构 */
export interface MapPolyline {
  readonly points: ReadonlyArray<{
    readonly latitude: number
    readonly longitude: number
  }>
  readonly color: string
  readonly width: number
  readonly arrowLine: boolean
}

const MARKER_SIZE = 40
const ACTIVE_MARKER_SIZE = 60
const AED_MARKER_ONLINE = '/static/images/aed-marker-online.png'
const AED_MARKER_OFFLINE = '/static/images/aed-marker-offline.png'
const RIPPLE_RING_ICON = '/static/images/ripple-ring.png'

/** 格式化距离文本 */
export function formatDistance(distance: number): string {
  if (distance <= 0) return '距离未知'
  if (distance >= 1000) return `约${(distance / 1000).toFixed(1)}km`
  return `约${Math.round(distance)}m`
}

/** AED 列表转 markers */
export function aedListToMarkers(aedList: readonly AedItem[]): MapMarker[] {
  return aedList.map((aed, index) => ({
    id: index,
    latitude: aed.lat,
    longitude: aed.lng,
    iconPath: aed.status === 'online'
      ? AED_MARKER_ONLINE
      : AED_MARKER_OFFLINE,
    width: MARKER_SIZE,
    height: MARKER_SIZE,
    anchor: { x: 0.5, y: 0.5 },
  }))
}

/** 生成高亮态 marker（最近 AED 放大） */
export function createActiveMarker(
  aed: AedItem,
  markerId: number,
): MapMarker {
  return {
    id: markerId,
    latitude: aed.lat,
    longitude: aed.lng,
    iconPath: AED_MARKER_ONLINE,
    width: ACTIVE_MARKER_SIZE,
    height: ACTIVE_MARKER_SIZE,
    anchor: { x: 0.5, y: 0.5 },
  }
}

/** 生成呼吸动画帧 marker（指定尺寸） */
export function createBreathingMarker(
  aed: AedItem,
  markerId: number,
  size: number,
): MapMarker {
  return {
    id: markerId,
    latitude: aed.lat,
    longitude: aed.lng,
    iconPath: AED_MARKER_ONLINE,
    width: size,
    height: size,
    anchor: { x: 0.5, y: 0.5 },
  }
}

// ===== 呼吸动画辅助 =====
const BREATHE_BASE = 60
const BREATHE_AMPLITUDE = 5

/** 正弦波平滑呼吸尺寸（step / totalSteps 为一个周期） */
export function getBreathSize(step: number, totalSteps: number): number {
  const phase = ((step % totalSteps) / totalSteps) * 2 * Math.PI
  return Math.round(BREATHE_BASE + BREATHE_AMPLITUDE * Math.sin(phase))
}

// ===== 涟漪动画辅助 =====
const RIPPLE_MIN_SIZE = 50
const RIPPLE_MAX_SIZE = 96
export const RIPPLE_ID_BASE = 990

function easeOutCubic(progress: number): number {
  return 1 - (1 - progress) ** 3
}

/** 生成涟漪环 marker */
export function createRippleMarker(
  lat: number,
  lng: number,
  ringIndex: number,
  size: number,
): MapMarker {
  return {
    id: RIPPLE_ID_BASE + ringIndex,
    latitude: lat,
    longitude: lng,
    iconPath: RIPPLE_RING_ICON,
    width: size,
    height: size,
    anchor: { x: 0.5, y: 0.5 },
  }
}

/** 计算涟漪环在 step 时的尺寸（按 ringIndex 错开相位） */
export function getRippleSize(
  step: number,
  totalSteps: number,
  ringIndex: number,
  ringCount: number,
): number {
  const offset = ringIndex * (totalSteps / ringCount)
  const phase = ((step + offset) % totalSteps) / totalSteps
  const easedPhase = easeOutCubic(phase)
  return Math.round(
    RIPPLE_MIN_SIZE + (RIPPLE_MAX_SIZE - RIPPLE_MIN_SIZE) * easedPhase,
  )
}

/** 构建导航 polyline */
export function createRoutePolyline(
  points: readonly GeoPoint[],
): MapPolyline {
  return {
    points: points.map((point) => ({
      latitude: point.lat,
      longitude: point.lng,
    })),
    color: '#1890FF',
    width: 6,
    arrowLine: true,
  }
}

/**
 * 调用腾讯地图路线规划 API
 * 返回路径点序列
 */
export async function fetchWalkingRoute(
  from: GeoPoint,
  to: GeoPoint,
): Promise<GeoPoint[]> {
  const mapKey = import.meta.env.VITE_TENCENT_MAP_KEY
  if (!mapKey || mapKey === 'YOUR_MAP_KEY_HERE') {
    console.warn('[MapService] 腾讯地图 Key 未配置，跳过路线规划')
    return []
  }

  const url = `https://apis.map.qq.com/ws/direction/v1/walking/?from=${from.lat},${from.lng}&to=${to.lat},${to.lng}&key=${mapKey}`

  return new Promise((resolve) => {
    uni.request({
      url,
      success: (res) => {
        const data = res.data as {
          status: number
          result?: {
            routes?: Array<{
              polyline?: number[]
            }>
          }
        }
        if (data.status !== 0 || !data.result?.routes?.[0]?.polyline) {
          console.warn('[MapService] 路线规划无结果')
          resolve([])
          return
        }

        const polyline = data.result.routes[0].polyline!
        resolve(decodePolyline(polyline))
      },
      fail: () => {
        console.warn('[MapService] 路线规划请求失败')
        resolve([])
      },
    })
  })
}

/** 解码腾讯地图压缩 polyline */
function decodePolyline(polyline: number[]): GeoPoint[] {
  const points: GeoPoint[] = []
  for (let i = 2; i < polyline.length; i++) {
    polyline[i] = polyline[i - 2] + polyline[i] / 1000000
  }
  for (let i = 0; i < polyline.length; i += 2) {
    points.push({ lat: polyline[i], lng: polyline[i + 1] })
  }
  return points
}

// ===== 地图上下文操作 =====

/**
 * 创建地图上下文
 * 页面中 `<map id="aedMap">` 对应
 */
export function createMapCtx(mapId = 'aedMap'): UniApp.MapContext {
  return uni.createMapContext(mapId)
}

/** 移动到用户位置 */
export function moveToUserLocation(mapCtx: UniApp.MapContext): void {
  mapCtx.moveToLocation({
    success: () => { /* noop */ },
    fail: () => console.warn('[MapService] moveToLocation 失败'),
  })
}

/**
 * 调用原生导航（wx.openLocation 兜底方案）
 * 点击 AED 卡片"导航"按钮时使用
 */
export function openNativeNavigation(
  lat: number,
  lng: number,
  name: string,
  address: string,
): void {
  uni.openLocation({
    latitude: lat,
    longitude: lng,
    name,
    address,
    scale: 18,
  })
}
