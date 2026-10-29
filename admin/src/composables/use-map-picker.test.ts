import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AMapModule } from '@/types/amap'
import { siteConfig } from '@/config/site-config'
import { loadAmap } from '@/utils/amap-loader'
import { useMapPicker } from './use-map-picker'

vi.mock('@/utils/amap-loader', () => ({ loadAmap: vi.fn() }))

interface SearchCityCapture {
  city?: string
}

function createFakeAmap(captured: {
  autoComplete: SearchCityCapture[]
  placeSearch: SearchCityCapture[]
}): AMapModule {
  class FakeMap {
    add(): void {}
    on(): void {}
    setCenter(): void {}
    destroy(): void {}
  }
  class FakeMarker {
    on(): void {}
    setPosition(): void {}
  }
  class FakeGeocoder {
    getAddress(): void {}
  }
  class FakeAutoComplete {
    constructor(options: SearchCityCapture) {
      captured.autoComplete.push(options)
    }
    search(): void {}
  }
  class FakePlaceSearch {
    constructor(options: SearchCityCapture) {
      captured.placeSearch.push(options)
    }
    search(): void {}
  }
  class FakeLngLat {
    constructor(readonly lng: number, readonly lat: number) {}
    getLng(): number { return this.lng }
    getLat(): number { return this.lat }
  }

  return {
    Map: FakeMap,
    Marker: FakeMarker,
    Geocoder: FakeGeocoder,
    AutoComplete: FakeAutoComplete,
    PlaceSearch: FakePlaceSearch,
    LngLat: FakeLngLat,
  } as unknown as AMapModule
}

describe('useMapPicker 站点配置与地图凭据错误', () => {
  beforeEach(() => {
    vi.mocked(loadAmap).mockReset()
  })

  it('检索城市来自 config/site.json', async () => {
    const captured = { autoComplete: [] as SearchCityCapture[], placeSearch: [] as SearchCityCapture[] }
    vi.mocked(loadAmap).mockResolvedValue(createFakeAmap(captured))

    const picker = useMapPicker()
    await picker.initMap('map', siteConfig.mapCenter.latitude, siteConfig.mapCenter.longitude)

    expect(captured.autoComplete[0]?.city).toBe(siteConfig.searchCity)
    expect(captured.placeSearch[0]?.city).toBe(siteConfig.searchCity)
    expect(picker.error.value).toBe('')
  })

  it('地图 Key 缺失时把明确配置错误暴露到 error，而不是静默显示地图', async () => {
    vi.mocked(loadAmap).mockRejectedValue(
      new Error('高德地图 Key 未配置，请在 .env 文件中设置 VITE_AMAP_KEY'),
    )

    const picker = useMapPicker()
    await picker.initMap('map', 0, 0)

    expect(picker.error.value).toContain('VITE_AMAP_KEY')
    expect(picker.loading.value).toBe(false)
  })
})
