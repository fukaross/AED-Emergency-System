// 定位、授权、距离计算 —— 全程使用 GCJ-02 坐标，别自己瞎转
import type { GeoPoint } from '@/types/common'
import { MAP_CENTER } from '@/utils/constants'
import { APP_ENV, USE_MOCK, USE_REAL_LOCATION } from '@/utils/runtime-env'

export { calculateDistance, findNearestOnlineAed } from '@/services/geo-distance'

const DEVTOOLS_PLATFORM = 'devtools'
const LOCATION_SCOPE = 'scope.userLocation'
const LOCATION_TIMEOUT_MS = 10000
type GetCurrentLocationMode = 'auto' | 'interactive'
let hasShownLocationPermissionGuide = false

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

const SHOULD_USE_DEFAULT_LOCATION = shouldUseDefaultLocation(APP_ENV, USE_MOCK, USE_REAL_LOCATION)

export interface LocationResult {
  readonly success: boolean
  readonly point: GeoPoint
  readonly isDefault: boolean
  readonly showSystemLocation: boolean
  readonly failureReason?: LocationFailureReason
  readonly address?: string
  readonly name?: string
  readonly errorMessage?: string
}

export type LocationFailureReason = 'permission_denied' | 'unavailable' | 'cancelled'

interface GetCurrentLocationOptions {
  readonly mode?: GetCurrentLocationMode
  readonly timeoutMs?: number
}

function createDefaultLocation(
  errorMessage: string,
  failureReason: LocationFailureReason = 'unavailable',
): LocationResult {
  return {
    success: false,
    point: { lat: MAP_CENTER.latitude, lng: MAP_CENTER.longitude },
    isDefault: true,
    showSystemLocation: false,
    failureReason,
    errorMessage,
  }
}

function getRuntimePlatform(): string {
  const runtime = uni as typeof uni & {
    readonly getDeviceInfo?: () => DeviceInfoLike
    readonly getSystemInfoSync?: () => DeviceInfoLike
  }

  const platform = runtime.getDeviceInfo?.().platform || runtime.getSystemInfoSync?.().platform

  return platform || ''
}

function buildLocationFailureMessage(errMsg: string): string {
  if (errMsg.includes('auth deny') || errMsg.includes('authorize')) {
    return '定位权限被拒绝，请开启定位权限后重试'
  }

  if (errMsg.includes('timeout')) {
    return '定位超时，请重试'
  }

  return '定位失败，请重试'
}

async function ensureLocationPermission(mode: GetCurrentLocationMode): Promise<boolean> {
  const setting = (await uni.getSetting()) as AuthSettingResult
  const authorized = setting.authSetting?.[LOCATION_SCOPE]

  if (authorized === true) {
    return true
  }

  if (authorized === false) {
    showLocationPermissionGuideOnce(mode)
    return false
  }

  try {
    await uni.authorize({ scope: LOCATION_SCOPE })
    return true
  } catch (err) {
    const errMsg = (err as { errMsg?: string })?.errMsg || ''
    console.warn('[GeoService] authorize failed:', errMsg)

    if (errMsg.includes('auth deny') || errMsg.includes('authorize')) {
      showLocationPermissionGuideOnce(mode)
    }

    return false
  }
}

function buildLocationOptions(mode: GetCurrentLocationMode): GetLocationWithAccuracyOptions {
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
    new Promise<never>((_, reject) => setTimeout(() => reject(new Error(`${label}: timeout`)), ms)),
  ])
}

async function requestCurrentLocation(
  mode: GetCurrentLocationMode,
  timeoutMs?: number,
): Promise<LocationResult> {
  const hasPermission = await ensureLocationPermission(mode)
  if (!hasPermission) {
    return createDefaultLocation('定位权限被拒绝，请开启定位权限后重试', 'permission_denied')
  }

  try {
    const res = await withTimeout(
      uni.getLocation(buildLocationOptions(mode)),
      timeoutMs ?? LOCATION_TIMEOUT_MS,
      'getLocation',
    )

    return {
      success: true,
      point: { lat: res.latitude, lng: res.longitude },
      isDefault: false,
      showSystemLocation: true,
    }
  } catch (err) {
    const errMsg = (err as { errMsg?: string })?.errMsg || (err as Error)?.message || ''
    console.warn('[GeoService] getLocation failed:', errMsg)

    if (errMsg.includes('auth deny') || errMsg.includes('authorize')) {
      showLocationPermissionGuideOnce(mode)
    }

    const denied = errMsg.includes('auth deny') || errMsg.includes('authorize')
    return createDefaultLocation(
      buildLocationFailureMessage(errMsg),
      denied ? 'permission_denied' : 'unavailable',
    )
  }
}

export async function chooseLocationPoint(center?: GeoPoint): Promise<LocationResult> {
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
      showLocationPermissionGuideOnce('interactive')
    }

    if (errMsg.includes('cancel')) {
      return createDefaultLocation('未选择位置，请重试', 'cancelled')
    }

    if (errMsg.includes('auth deny')) {
      return createDefaultLocation('位置选择权限被拒绝，请开启位置权限后重试', 'permission_denied')
    }

    return createDefaultLocation('位置选择失败，请重试')
  }
}

/** 获取当前位置 (GCJ-02)；失败结果携带校园中心占位坐标，由调用方决定是否使用。 */
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
function showLocationPermissionGuideOnce(mode: GetCurrentLocationMode): void {
  if (mode !== 'interactive' || hasShownLocationPermissionGuide) return
  hasShownLocationPermissionGuide = true
  uni.showModal({
    title: '需要位置权限',
    content: '请开启位置权限，用于查找附近 AED 和急救会话位置共享',
    confirmText: '去设置',
    success: (res) => {
      if (res.confirm) {
        uni.openSetting({})
      }
    },
  })
}
