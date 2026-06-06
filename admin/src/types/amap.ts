export interface AmapLngLat {
  getLng(): number
  getLat(): number
}

export interface AmapMarker {
  setPosition(lnglat: AmapLngLat): void
  on(event: string, handler: (...args: unknown[]) => void): void
  remove(): void
}

export type GeocoderCallback = (status: string, result: { regeocode: { formattedAddress: string } }) => void

export interface AmapGeocoder {
  getAddress(lnglat: AmapLngLat | [number, number], callback: GeocoderCallback): void
}

export type AutoCompleteCallback = (status: string, result: { tips: AutocompleteTip[] }) => void

export interface AutocompleteTip {
  name: string
  district: string
  address: string
  location?: AmapLngLat
  id?: string
}

export interface AmapAutoComplete {
  search(keyword: string, callback: AutoCompleteCallback): void
}

export interface AmapPlaceSearch {
  search(keyword: string, callback: (status: string, result: { pois: AmapPoi[] }) => void): void
}

export interface AmapPoi {
  name: string
  address: string
  location: AmapLngLat
}

export interface AmapEvent {
  lnglat: AmapLngLat
}

export interface AmapMap {
  on(event: string, handler: (e: AmapEvent) => void): void
  setCenter(lnglat: [number, number]): void
  destroy(): void
  add(marker: AmapMarker): void
  remove(marker: AmapMarker): void
}

export interface AMapModule {
  Map: new (container: string, options: { zoom: number; center: [number, number]; resizeEnable?: boolean; viewMode?: string }) => AmapMap
  Marker: new (options: { position: [number, number]; draggable?: boolean }) => AmapMarker
  Geocoder: new () => AmapGeocoder
  AutoComplete: new (options: { city?: string }) => AmapAutoComplete
  PlaceSearch: new (options: { city?: string; pageSize?: number }) => AmapPlaceSearch
  LngLat: new (lng: number, lat: number) => AmapLngLat
}
