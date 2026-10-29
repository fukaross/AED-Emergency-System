// 通用类型 —— 后端响应格式、分页、坐标、认证、上传

export interface ApiResponse<T = unknown> {
  readonly code: number
  readonly message: string
  readonly data: T
}

export interface PaginationParams {
  readonly page?: number
  readonly pageSize?: number
}

export interface PaginatedData<T> {
  readonly items: readonly T[]
  readonly total: number
  readonly page: number
  readonly pageSize: number
  readonly totalPages: number
}

// 使用 GCJ-02 坐标系
export interface GeoPoint {
  readonly lat: number
  readonly lng: number
}

export interface LoginRequest {
  readonly code: string
}

export interface LoginResponse {
  readonly token: string
  readonly expiresIn: number
  readonly userInfo: {
    readonly id: string
    readonly name: string
    readonly avatarUrl: string
    readonly isNewUser: boolean
  }
}

export interface UploadImageResponse {
  readonly url: string
  readonly thumbnail: string
  readonly fileId: string
}

export type UploadScene = 'avatar' | 'report' | 'help' | 'volunteer' | 'session'

