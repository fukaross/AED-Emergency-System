import { describe, it, expect } from 'vitest'
import {
  ADMIN_MAP_CENTER,
  lngLatToPoint,
  pointToLngLat,
  isValidCoordinate,
  getDisplayCenter,
} from './map-coordinate'

// 合成坐标：与共享站点配置无关的任意合法点，用于纯坐标转换/校验断言。
const SAMPLE_LNG = 121.501
const SAMPLE_LAT = 31.244

describe('map-coordinate', () => {
  describe('lngLatToPoint', () => {
    it('converts [lng, lat] to { lat, lng }', () => {
      expect(lngLatToPoint([SAMPLE_LNG, SAMPLE_LAT])).toEqual({ lat: SAMPLE_LAT, lng: SAMPLE_LNG })
    })
  })

  describe('pointToLngLat', () => {
    it('converts { lat, lng } to [lng, lat]', () => {
      expect(pointToLngLat(SAMPLE_LAT, SAMPLE_LNG)).toEqual([SAMPLE_LNG, SAMPLE_LAT])
    })
  })

  describe('isValidCoordinate', () => {
    it('returns false for 0/0', () => {
      expect(isValidCoordinate(0, 0)).toBe(false)
    })

    it('returns false for near-zero', () => {
      expect(isValidCoordinate(0.001, 0.001)).toBe(false)
    })

    it('returns false for out-of-range lat', () => {
      expect(isValidCoordinate(91, 110)).toBe(false)
      expect(isValidCoordinate(-91, 110)).toBe(false)
    })

    it('returns false for out-of-range lng', () => {
      expect(isValidCoordinate(20, 181)).toBe(false)
      expect(isValidCoordinate(20, -181)).toBe(false)
    })

    it('returns true for valid coordinates', () => {
      expect(isValidCoordinate(SAMPLE_LAT, SAMPLE_LNG)).toBe(true)
    })
  })

  describe('getDisplayCenter', () => {
    it('returns actual coordinates when valid', () => {
      expect(getDisplayCenter(SAMPLE_LAT, SAMPLE_LNG)).toEqual([SAMPLE_LNG, SAMPLE_LAT])
    })

    it('returns ADMIN_MAP_CENTER for 0/0', () => {
      expect(getDisplayCenter(0, 0)).toEqual([
        ADMIN_MAP_CENTER.lng,
        ADMIN_MAP_CENTER.lat,
      ])
    })
  })
})
