/**
 * 公共 API（上传、认证）
 *
 * 接口来源：CLAUDE.md §8.1 + BackEnd.md §4.2
 *
 * POST /api/upload/image   — 图片上传 [登录可选]
 * POST /api/auth/login     — 微信登录 [公开]
 */
import { uploadFile } from '@/services/request-upload'
import type { UploadImageResponse, UploadScene } from '@/types/common'

/** 上传图片 */
export function uploadImage(
  filePath: string,
  scene: UploadScene,
): Promise<UploadImageResponse> {
  return uploadFile(filePath, scene)
}
