import client from './client'
import type { ApiResponse } from '@/types/api'
import type {
  AdminAedDeviceItem,
  AdminAedDeviceListResponse,
  CreateAedDevicePayload,
  UpdateAedDevicePayload,
  DeleteAedDeviceResponse,
} from '@/types/device'

export interface AedDeviceListParams {
  page?: number
  pageSize?: number
  status?: string
  keyword?: string
}

export function getAedDevices(params: AedDeviceListParams = {}) {
  return client.get<ApiResponse<AdminAedDeviceListResponse>>('/aed-devices', { params })
}

export function getAedDeviceDetail(id: string) {
  return client.get<ApiResponse<AdminAedDeviceItem>>(`/aed-devices/${id}`)
}

export function createAedDevice(data: CreateAedDevicePayload) {
  return client.post<ApiResponse<AdminAedDeviceItem>>('/aed-devices', data)
}

export function updateAedDevice(id: string, data: UpdateAedDevicePayload) {
  return client.put<ApiResponse<AdminAedDeviceItem>>(`/aed-devices/${id}`, data)
}

export function deleteAedDevice(id: string) {
  return client.delete<ApiResponse<DeleteAedDeviceResponse>>(`/aed-devices/${id}`)
}
