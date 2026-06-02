export const MAX_VIDEO_SIZE_MB = 100

const BYTES_PER_MB = 1024 * 1024

export type AdminVideoUploadScene = 'course'

export interface AdminVideoUploadResult {
  url: string
  thumbnail: string
  fileId: string
}

interface UploadApiResponse {
  code: number
  message?: string
  data?: AdminVideoUploadResult
}

export function validateAdminVideoFile(file: File): string | null {
  if (file.type !== 'video/mp4') return '只能上传 MP4 视频'
  if (file.size > MAX_VIDEO_SIZE_MB * BYTES_PER_MB) {
    return `视频大小不能超过 ${MAX_VIDEO_SIZE_MB}MB`
  }
  return null
}

export async function uploadAdminVideo(
  file: File,
  scene: AdminVideoUploadScene,
): Promise<AdminVideoUploadResult> {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('scene', scene)

  const response = await fetch('/api/upload/video', {
    method: 'POST',
    headers: { 'X-Aed-Admin-Key': localStorage.getItem('adminKey') || '' },
    body: formData,
  })
  const result = await readUploadResponse(response)
  if (!response.ok || result.code !== 0 || !result.data?.url) {
    throw new Error(result.message || `视频上传失败（HTTP ${response.status}）`)
  }
  return result.data
}

async function readUploadResponse(response: Response): Promise<UploadApiResponse> {
  const text = await response.text()
  if (!text.trim()) {
    return {
      code: response.ok ? 0 : response.status,
      message: response.ok ? undefined : `视频上传失败（HTTP ${response.status}）`,
    }
  }
  return JSON.parse(text) as UploadApiResponse
}
