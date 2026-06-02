export const MAX_IMAGE_SIZE_MB = 5

const BYTES_PER_MB = 1024 * 1024

export type AdminImageUploadScene = 'course' | 'quick-action'

export interface AdminImageUploadResult {
  url: string
  thumbnail: string
  fileId: string
}

interface UploadApiResponse {
  code: number
  message?: string
  data?: AdminImageUploadResult
}

export function validateAdminImageFile(file: File): string | null {
  if (!file.type.startsWith('image/')) return '只能上传图片文件'
  if (file.size > MAX_IMAGE_SIZE_MB * BYTES_PER_MB) {
    return `图片大小不能超过 ${MAX_IMAGE_SIZE_MB}MB`
  }
  return null
}

export async function uploadAdminImage(
  file: File,
  scene: AdminImageUploadScene,
): Promise<AdminImageUploadResult> {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('scene', scene)

  const response = await fetch('/api/upload/image', {
    method: 'POST',
    headers: { 'X-Aed-Admin-Key': localStorage.getItem('adminKey') || '' },
    body: formData,
  })
  const result = await response.json() as UploadApiResponse
  if (!response.ok || result.code !== 0 || !result.data?.url) {
    throw new Error(result.message || '图片上传失败')
  }
  return result.data
}
