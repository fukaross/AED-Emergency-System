// 公共服务接口 —— 图片上传
import { uploadFile } from '@/services/request-upload'
import type { UploadImageResponse, UploadScene } from '@/types/common'

/** 上传图片 */
export function uploadImage(
  filePath: string,
  scene: UploadScene,
): Promise<UploadImageResponse> {
  return uploadFile(filePath, scene)
}
