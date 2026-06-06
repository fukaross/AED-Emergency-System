import AMapLoader from '@amap/amap-jsapi-loader'
import type { AMapModule } from '@/types/amap'

declare global {
  interface Window {
    _AMapSecurityConfig?: {
      securityJsCode: string
    }
  }
}

let loaderPromise: Promise<AMapModule> | null = null

export function loadAmap(): Promise<AMapModule> {
  if (loaderPromise) return loaderPromise

  const key = import.meta.env.VITE_AMAP_KEY
  if (!key) {
    throw new Error('高德地图 Key 未配置，请在 .env 文件中设置 VITE_AMAP_KEY')
  }

  const securityCode = import.meta.env.VITE_AMAP_SECURITY_CODE
  if (securityCode) {
    window._AMapSecurityConfig = { securityJsCode: securityCode }
  }

  loaderPromise = AMapLoader.load({
    key,
    version: '2.0',
    plugins: ['AMap.Geocoder', 'AMap.AutoComplete', 'AMap.PlaceSearch'],
  }) as unknown as Promise<AMapModule>

  return loaderPromise
}
