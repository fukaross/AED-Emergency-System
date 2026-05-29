// 定位、授权、距离计算 —— 全程使用 GCJ-02 坐标，别自己瞎转
import type { GeoPoint } from '@/types/common'
import { MAP_CENTER } from '@/utils/constants'
import { APP_ENV, USE_MOCK, USE_REAL_LOCATION } from '@/utils/runtime-env'

const EARTH_RADIUS_M = 6371000
const DEVTOOLS_PLATFORM = 'devtools'
const LOCATION_SCOPE = 'scope.userLocation'
const LOCATION_TIMEOUT_MS = 10000
type GetCurrentLocationMode = 'auto' | 'interactive'

interface DeviceInfoLike {
  readonly platform?: string
}

interface AuthSettingResult {
  readonly authSetting?: Partial<Record<typeof LOCATION_SCOPE, boolean>>
}

type GetLocationWithAccuracyOptions = UniApp.GetLocationOptions & {
  readonly isHighAccuracy?: boolean
  readonly highAccuracyExpireTime?: number
}

export function shouldUseDefaultLocation(
  appEnv: string,
  useMock: boolean,
  useRealLocation: boolean,
): boolean {
  return appEnv === 'preview' && useMock && !useRealLocation
}

export function shouldSkipInitialRealLocation(
  appEnv: string,
  useMock: boolean,
  useRealLocation: boolean,
  autoLocateOnInit: boolean,
): boolean {
  return appEnv === 'preview' && useMock && useRealLocation && !autoLocateOnInit
}

export function isWechatDevtoolsPlatform(platform: string): boolean {
  return platform === DEVTOOLS_PLATFORM
}

export function shouldUseManualLocationPicker(
  platform: string,
  mode: GetCurrentLocationMode,
): boolean {
  return isWechatDevtoolsPlatform(platform) && mode === 'interactive'
}

const SHOULD_USE_DEFAULT_LOCATION = shouldUseDefaultLocation(
  APP_ENV,
  USE_MOCK,
  USE_REAL_LOCATION,
)

/** Haversine 公式计算两点间球面距离 (米) */
export function calculateDistance(a: GeoPoint, b: GeoPoint): number {
  const toRad = (deg: number) => (deg * Math.PI) / 180
  const dLat = toRad(b.lat - a.lat)
  const dLng = toRad(b.lng - a.lng)
  const sinLat = Math.sin(dLat / 2)
  const sinLng = Math.sin(dLng / 2)
  const h =
    sinLat * sinLat +
    Math.cos(toRad(a.lat)) * Math.cos(toRad(b.lat)) * sinLng * sinLng
  return 2 * EARTH_RADIUS_M * Math.asin(Math.sqrt(h))
}

export interface LocationResult {
  readonly success: boolean
  readonly point: GeoPoint
  readonly isDefault: boolean
  readonly showSystemLocation: boolean
  readonly address?: string
  readonly name?: string
  readonly errorMessage?: string
}

interface GetCurrentLocationOptions {
  readonly mode?: GetCurrentLocationMode
  readonly timeoutMs?: number
}

function createDefaultLocation(errorMessage: string): LocationResult {
  return {
    success: false,
    point: { lat: MAP_CENTER.latitude, lng: MAP_CENTER.longitude },
    isDefault: true,
    showSystemLocation: false,
    errorMessage,
  }
}

function getRuntimePlatform(): string {
  const runtime = uni as typeof uni & {
    readonly getDeviceInfo?: () => DeviceInfoLike
    readonly getSystemInfoSync?: () => DeviceInfoLike
  }

  const platform =
    runtime.getDeviceInfo?.().platform || runtime.getSystemInfoSync?.().platform

  return platform || ''
}

function buildLocationFailureMessage(errMsg: string): string {
  if (errMsg.includes('auth deny')) {
    return '定位权限被拒绝，已使用校园中心点'
  }

  if (errMsg.includes('timeout')) {
    return '定位超时，已使用校园中心点'
  }

  return '定位失败，已使用校园中心点'
}

async function ensureLocationPermission(): Promise<boolean> {
  const setting = (await uni.getSetting()) as AuthSettingResult
  const authorized = setting.authSetting?.[LOCATION_SCOPE]

  if (authorized === true) {
    return true
  }

  try {
    await uni.authorize({ scope: LOCATION_SCOPE })
    return true
  } catch (err) {
    const errMsg = (err as { errMsg?: string })?.errMsg || ''
    console.warn('[GeoService] authorize failed:', errMsg)

    if (errMsg.includes('auth deny') || errMsg.includes('authorize')) {
      showLocationPermissionGuide()
    }

    return false
  }
}

function buildLocationOptions(
  mode: GetCurrentLocationMode,
): GetLocationWithAccuracyOptions {
  if (mode === 'interactive') {
    return {
      type: 'gcj02',
      isHighAccuracy: true,
      highAccuracyExpireTime: LOCATION_TIMEOUT_MS,
    }
  }

  return { type: 'gcj02' }
}

function withTimeout<T>(promise: Promise<T>, ms: number, label: string): Promise<T> {
  return Promise.race([
    promise,
    new Promise<never>((_, reject) =>
      setTimeout(() => reject(new Error(`${label}: timeout`)), ms),
    ),
  ])
}

async function requestCurrentLocation(
  mode: GetCurrentLocationMode,
  timeoutMs?: number,
): Promise<LocationResult> {
  const hasPermission = await ensureLocationPermission()
  if (!hasPermission) {
    return createDefaultLocation('定位权限被拒绝，已使用校园中心点')
  }

  try {
    const res = await withTimeout(
      uni.getLocation(buildLocationOptions(mode)),
      timeoutMs ?? LOCATION_TIMEOUT_MS,
      'getLocation',
    )

    console.log(
      `[GeoService] 定位成功 lat=${res.latitude} lng=${res.longitude} accuracy=${(res as { accuracy?: number }).accuracy ?? '未知'}m`,
    )

    return {
      success: true,
      point: { lat: res.latitude, lng: res.longitude },
      isDefault: false,
      showSystemLocation: true,
    }
  } catch (err) {
    const errMsg = (err as { errMsg?: string })?.errMsg
      || (err as Error)?.message
      || ''
    console.warn('[GeoService] getLocation failed:', errMsg)

    if (errMsg.includes('auth deny') || errMsg.includes('authorize')) {
      showLocationPermissionGuide()
    }

    return createDefaultLocation(buildLocationFailureMessage(errMsg))
  }
}

export async function chooseLocationPoint(
  center?: GeoPoint,
): Promise<LocationResult> {
  try {
    const res = await uni.chooseLocation({
      latitude: center?.lat ?? MAP_CENTER.latitude,
      longitude: center?.lng ?? MAP_CENTER.longitude,
    })

    return {
      success: true,
      point: { lat: res.latitude, lng: res.longitude },
      isDefault: false,
      showSystemLocation: false,
      address: res.address,
      name: res.name,
    }
  } catch (err) {
    const errMsg = (err as { errMsg?: string })?.errMsg || ''
    console.warn('[GeoService] chooseLocation failed:', errMsg)

    if (errMsg.includes('auth deny') || errMsg.includes('authorize')) {
      showLocationPermissionGuide()
    }

    if (errMsg.includes('cancel')) {
      return createDefaultLocation('未选择位置，请重试')
    }

    if (errMsg.includes('auth deny')) {
      return createDefaultLocation('位置选择权限被拒绝，请开启位置权限后重试')
    }

    return createDefaultLocation('位置选择失败，请重试')
  }
}

/** 获取当前位置 (GCJ-02)。降级：devtools interactive→手动选点；权限拒绝/超时→校园中心 */
export async function getCurrentLocation(
  options: GetCurrentLocationOptions = {},
): Promise<LocationResult> {
  if (SHOULD_USE_DEFAULT_LOCATION) {
    return createDefaultLocation('预览模式未启用真实定位，已使用校园中心点')
  }

  const mode = options.mode || 'auto'
  const platform = getRuntimePlatform()

  if (shouldUseManualLocationPicker(platform, mode)) {
    return chooseLocationPoint()
  }

  return requestCurrentLocation(mode, options.timeoutMs)
}

/** 引导用户开启定位权限 */
function showLocationPermissionGuide(): void {
  uni.showModal({
    title: '需要位置权限',
    content: '请在系统设置中开启位置权限，以便查找附近 AED 设备',
    confirmText: '去设置',
    success: (res) => {
      if (res.confirm) {
        uni.openSetting({})
      }
    },
  })
}

/** 从 AED 列表中找出距离用户最近的在线 AED */
export function findNearestOnlineAed<
  T extends { lat: number; lng: number; status: string },
>(aedList: readonly T[], userPoint: GeoPoint): T | null {
  let nearest: T | null = null
  let minDist = Infinity

  for (const aed of aedList) {
    if (aed.status !== 'online') continue
    const dist = calculateDistance(userPoint, { lat: aed.lat, lng: aed.lng })
    if (dist < minDist) {
      minDist = dist
      nearest = aed
    }
  }

  return nearest
}
