/**
 * 通用类型定义
 *
 * 后端通用响应格式、分页、错误码、认证相关 DTO
 * 来源：BackEnd.md §4.1 + CLAUDE.md §8.2
 */

/** 后端统一响应包装 */
export interface ApiResponse<T = unknown> {
  readonly code: number
  readonly message: string
  readonly data: T
}

/** 分页请求参数 */
export interface PaginationParams {
  readonly page?: number
  readonly pageSize?: number
}

/** 分页响应数据 */
export interface PaginatedData<T> {
  readonly items: readonly T[]
  readonly total: number
  readonly page: number
  readonly pageSize: number
  readonly totalPages: number
}

/** 地理坐标点 (GCJ-02) */
export interface GeoPoint {
  readonly lat: number
  readonly lng: number
}

/** 登录请求 */
export interface LoginRequest {
  readonly code: string
}

/** 登录响应 */
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

/** 图片上传响应 */
export interface UploadImageResponse {
  readonly url: string
  readonly thumbnail: string
  readonly fileId: string
}

/** 上传场景 */
export type UploadScene = 'avatar' | 'report' | 'help'

/** 轮播横幅 */
export interface BannerItem {
  readonly id: string
  readonly image: string
  readonly title: string
  readonly linkType: 'course' | 'url' | 'page' | 'none'
  readonly linkTarget: string
  readonly sortOrder: number
}

/** Banner 跳转类型 */
export type BannerLinkType = BannerItem['linkType']
