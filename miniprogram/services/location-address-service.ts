import type { GeoPoint } from '@/types/common'

const GEOCODER_URL = 'https://apis.map.qq.com/ws/geocoder/v1/'
const PLACEHOLDER_MAP_KEY = 'YOUR_MAP_KEY_HERE'

export interface ReverseGeocodeResponse {
  readonly status: number
  readonly result?: {
    readonly address?: string
    readonly formatted_addresses?: {
      readonly recommend?: string
    }
    readonly pois?: ReadonlyArray<{
      readonly title?: string
      readonly address?: string
    }>
  }
}

interface ResolveLocationAddressOptions {
  readonly point: GeoPoint
  readonly address?: string
  readonly name?: string
}

export function mergeLocationText(name?: string, address?: string): string {
  const trimmedName = name?.trim() || ''
  const trimmedAddress = address?.trim() || ''

  if (!trimmedName) return trimmedAddress
  if (!trimmedAddress) return trimmedName
  if (trimmedAddress.includes(trimmedName)) return trimmedAddress

  return `${trimmedName} ${trimmedAddress}`
}

export function pickReverseGeocodeAddress(
  payload: ReverseGeocodeResponse,
): string {
  if (payload.status !== 0 || !payload.result) {
    return ''
  }

  const recommend = payload.result.formatted_addresses?.recommend?.trim() || ''
  if (recommend) {
    return recommend
  }

  const firstPoi = payload.result.pois?.[0]
  const poiAddress = mergeLocationText(firstPoi?.title, firstPoi?.address)
  if (poiAddress) {
    return poiAddress
  }

  return payload.result.address?.trim() || ''
}

export async function resolveLocationAddress(
  options: ResolveLocationAddressOptions,
): Promise<string> {
  const directAddress = mergeLocationText(options.name, options.address)
  if (directAddress) {
    return directAddress
  }

  const mapKey = import.meta.env.VITE_TENCENT_MAP_KEY
  if (!mapKey || mapKey === PLACEHOLDER_MAP_KEY) {
    console.warn('[LocationAddressService] 腾讯地图 Key 未配置，跳过逆地理编码')
    return ''
  }

  const url = `${GEOCODER_URL}?location=${options.point.lat},${options.point.lng}&get_poi=1&key=${mapKey}`

  return new Promise((resolve) => {
    uni.request({
      url,
      success: (res) => {
        const address = pickReverseGeocodeAddress(
          (res.data as ReverseGeocodeResponse) || { status: -1 },
        )

        if (!address) {
          console.warn('[LocationAddressService] 逆地理编码无结果')
        }

        resolve(address)
      },
      fail: (err) => {
        console.warn(
          '[LocationAddressService] 逆地理编码失败:',
          err.errMsg || 'unknown error',
        )
        resolve('')
      },
    })
  })
}
