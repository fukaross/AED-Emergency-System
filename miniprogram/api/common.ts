/**
 * 公共 API（横幅、上传、认证）
 *
 * 接口来源：CLAUDE.md §8.1 + BackEnd.md §4.2/§4.8
 *
 * GET  /api/banners        — 轮播横幅 [公开]
 * POST /api/upload/image   — 图片上传 [登录可选]
 * POST /api/auth/login     — 微信登录 [公开]
 */
import { get } from '@/services/request'
import { uploadFile } from '@/services/request'
import type { BannerItem, UploadImageResponse, UploadScene } from '@/types/common'

/** 获取轮播横幅列表 */
export function fetchBanners(): Promise<BannerItem[]> {
  return get<BannerItem[]>('/api/banners')
}

/** 上传图片 */
export function uploadImage(
  filePath: string,
  scene: UploadScene,
): Promise<UploadImageResponse> {
  return uploadFile(filePath, scene)
}
