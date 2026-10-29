import { ref } from 'vue'
import type { AmapMap, AmapMarker, AmapGeocoder, AmapAutoComplete, AmapPlaceSearch, AmapLngLat, AutocompleteTip, AMapModule } from '@/types/amap'
import { loadAmap } from '@/utils/amap-loader'
import { getDisplayCenter, DEFAULT_ZOOM } from '@/utils/map-coordinate'
import { siteConfig } from '@/config/site-config'

interface SearchService {
  autocomplete: AmapAutoComplete | null
  placeSearch: AmapPlaceSearch | null
}

export function useMapPicker() {
  const loading = ref(false)
  const error = ref('')
  const suggestions = ref<AutocompleteTip[]>([])

  let mapInstance: AmapMap | null = null
  let markerInstance: AmapMarker | null = null
  let geocoder: AmapGeocoder | null = null
  let AMapMod: AMapModule | null = null
  const searchService: SearchService = { autocomplete: null, placeSearch: null }

  async function initMap(containerId: string, lat: number, lng: number, disabled?: boolean): Promise<void> {
    loading.value = true
    error.value = ''

    try {
      AMapMod = await loadAmap()
      const center = getDisplayCenter(lat, lng)

      mapInstance = new AMapMod.Map(containerId, {
        zoom: DEFAULT_ZOOM,
        center,
        resizeEnable: true,
        viewMode: '2D',
      })

      markerInstance = new AMapMod.Marker({
        position: center,
        draggable: !disabled,
      })

      mapInstance.add(markerInstance)
      geocoder = new AMapMod.Geocoder()
      searchService.autocomplete = new AMapMod.AutoComplete({ city: siteConfig.searchCity })
      searchService.placeSearch = new AMapMod.PlaceSearch({ city: siteConfig.searchCity, pageSize: 10 })

      if (!disabled) {
        mapInstance.on('click', (e: { lnglat: AmapLngLat }) => {
          const lng = e.lnglat.getLng()
          const latVal = e.lnglat.getLat()
          moveMarker(lng, latVal)
        })

        markerInstance.on('dragend', (e: unknown) => {
          const me = e as { lnglat: AmapLngLat }
          if (me?.lnglat) {
            const lng = me.lnglat.getLng()
            const latVal = me.lnglat.getLat()
            moveMarker(lng, latVal)
          }
        })
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : '高德地图加载失败'
    } finally {
      loading.value = false
    }
  }

  function moveMarker(lng: number, latVal: number): void {
    if (markerInstance && AMapMod) {
      markerInstance.setPosition(new AMapMod.LngLat(lng, latVal))
    }
    if (mapInstance) {
      mapInstance.setCenter([lng, latVal])
    }
    _onCoordChanged?.(latVal, lng)
    runReverseGeocode(latVal, lng)
  }

  function destroyMap(): void {
    if (mapInstance) {
      mapInstance.destroy()
      mapInstance = null
    }
    markerInstance = null
    geocoder = null
    searchService.autocomplete = null
    searchService.placeSearch = null
    suggestions.value = []
  }

  function setPosition(lat: number, lng: number): void {
    if (!markerInstance || !mapInstance || !AMapMod) return
    markerInstance.setPosition(new AMapMod.LngLat(lng, lat))
    mapInstance.setCenter([lng, lat])
    runReverseGeocode(lat, lng)
  }

  let _onCoordChanged: ((lat: number, lng: number) => void) | null = null
  let _onAddressChanged: ((address: string) => void) | null = null

  function onCoordChanged(callback: (lat: number, lng: number) => void): void {
    _onCoordChanged = callback
  }

  function onAddressChanged(callback: (address: string) => void): void {
    _onAddressChanged = callback
  }

  function runReverseGeocode(latVal: number, lngVal: number): void {
    if (!geocoder) return
    geocoder.getAddress([lngVal, latVal], (status, result) => {
      if (status === 'complete' && result.regeocode?.formattedAddress) {
        _onAddressChanged?.(result.regeocode.formattedAddress)
      }
    })
  }

  function searchKeyword(keyword: string): Promise<AutocompleteTip[]> {
    if (!searchService.autocomplete) {
      suggestions.value = []
      return Promise.resolve([])
    }

    return new Promise((resolve) => {
      searchService.autocomplete?.search(keyword, (status, result) => {
        if (status === 'complete' && result.tips) {
          suggestions.value = result.tips.filter(t => t.location || t.id)
        } else {
          suggestions.value = []
        }
        resolve(suggestions.value)
      })
    })
  }

  function selectSuggestion(tip: AutocompleteTip): Promise<void> {
    if (tip.location) {
      const lng = tip.location.getLng()
      const latVal = tip.location.getLat()
      setPosition(latVal, lng)
      _onCoordChanged?.(latVal, lng)
      _onAddressChanged?.(tip.name + (tip.district ? `，${tip.district}` : ''))
      suggestions.value = []
      return Promise.resolve()
    }

    if (searchService.placeSearch && tip.name) {
      return new Promise((resolve) => {
        searchService.placeSearch?.search(tip.name, (status, result) => {
          suggestions.value = []
          if (status === 'complete' && result.pois.length > 0) {
            const poi = result.pois[0]
            const lng = poi.location.getLng()
            const latVal = poi.location.getLat()
            setPosition(latVal, lng)
            _onCoordChanged?.(latVal, lng)
            _onAddressChanged?.(poi.name + (poi.address ? `，${poi.address}` : ''))
          }
          resolve()
        })
      })
    }

    suggestions.value = []
    return Promise.resolve()
  }

  return {
    loading,
    error,
    suggestions,
    initMap,
    destroyMap,
    setPosition,
    searchKeyword,
    selectSuggestion,
    onCoordChanged,
    onAddressChanged,
    getDisplayCenter: (lat: number, lng: number) => {
      const center = getDisplayCenter(lat, lng)
      return { lat: center[1], lng: center[0] }
    },
  }
}
