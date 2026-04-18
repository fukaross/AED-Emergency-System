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
  readonly callout?: {
    readonly content: string
    readonly display: string
  }
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
const AED_MARKER_ONLINE_PNG = '/static/images/aed-marker-online.png'
const AED_MARKER_OFFLINE_PNG = '/static/images/aed-marker-offline.png'

/** AED 列表转 markers */
export function aedListToMarkers(aedList: readonly AedItem[]): MapMarker[] {
  return aedList.map((aed, index) => ({
    id: index,
    latitude: aed.lat,
    longitude: aed.lng,
    iconPath: aed.status === 'online'
      ? AED_MARKER_ONLINE_PNG
      : AED_MARKER_OFFLINE_PNG,
    width: MARKER_SIZE,
    height: MARKER_SIZE,
    callout: {
      content: `${aed.name}\n${aed.status === 'online' ? '可用' : '不可用'}`,
      display: 'BYCLICK',
    },
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
    iconPath: AED_MARKER_ONLINE_PNG,
    width: ACTIVE_MARKER_SIZE,
    height: ACTIVE_MARKER_SIZE,
    callout: {
      content: `${aed.name}\n最近可用 AED`,
      display: 'ALWAYS',
    },
  }
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
