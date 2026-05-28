export interface AdminAedDeviceItem {
  id: string
  name: string
  lat: number
  lng: number
  status: 'online' | 'offline'
  statusNote: string | null
  image: string
  address: string
  lastMaintenance: string
  nextMaintenance: string
  maintainer: string
}

export interface AdminAedDeviceListResponse {
  items: AdminAedDeviceItem[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface CreateAedDevicePayload {
  name: string
  lat: number
  lng: number
  image?: string
  address?: string
  status?: 'online' | 'offline'
  statusNote?: string
  lastMaintenance?: string
  nextMaintenance?: string
  maintainer?: string
}

export interface UpdateAedDevicePayload {
  name?: string
  lat?: number
  lng?: number
  status?: 'online' | 'offline'
  statusNote?: string | null
  image?: string
  address?: string
  lastMaintenance?: string
  nextMaintenance?: string
  maintainer?: string
}

export interface DeleteAedDeviceResponse {
  deleted: boolean
}
