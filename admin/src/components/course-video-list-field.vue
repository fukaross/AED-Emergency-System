<template>
  <div>
    <div v-for="(video, idx) in modelValue" :key="idx" class="video-edit-card">
      <div class="video-header-row">
        <span class="video-label">视频 {{ idx + 1 }}</span>
        <div class="video-actions">
          <el-button text type="primary" size="small" :disabled="idx === 0" @click="moveVideo(idx, -1)">上移</el-button>
          <el-button text type="primary" size="small" :disabled="idx === modelValue.length - 1" @click="moveVideo(idx, 1)">下移</el-button>
          <el-button text type="primary" size="small" :disabled="!video.url" @click="openVideo(video.url)">预览</el-button>
          <el-button text type="danger" size="small" @click="removeVideo(idx)">删除</el-button>
        </div>
      </div>
      <el-form-item label="标题">
        <el-input v-model="video.title" placeholder="视频 1" />
      </el-form-item>
      <el-form-item label="地址">
        <el-input v-model="video.url" placeholder="视频 URL 或上传 MP4" />
      </el-form-item>
      <el-upload
        class="video-upload"
        drag
        :show-file-list="false"
        :http-request="createVideoUploadHandler(idx)"
        :before-upload="beforeVideoUpload"
        accept="video/mp4"
      >
        <div class="upload-dragger-content">
          <el-icon class="upload-icon"><UploadFilled /></el-icon>
          <div class="upload-text">将 MP4 视频拖到此处或点击上传</div>
        </div>
      </el-upload>
    </div>
    <el-button text type="primary" @click="addVideo">+ 添加视频</el-button>
  </div>
</template>

<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { UploadFilled } from '@element-plus/icons-vue'
import type { UploadRawFile, UploadRequestOptions } from 'element-plus'
import { uploadAdminVideo, validateAdminVideoFile } from '@/utils/admin-video-upload'
import type { CourseVideo } from '@/types/classroom'

const props = defineProps<{
  modelValue: CourseVideo[]
}>()

const emit = defineEmits<{
  (event: 'update:modelValue', value: CourseVideo[]): void
}>()

function addVideo() {
  emit('update:modelValue', [
    ...props.modelValue,
    { order: props.modelValue.length + 1, title: '', url: '' },
  ])
}

function removeVideo(idx: number) {
  emit('update:modelValue', props.modelValue.filter((_, index) => index !== idx))
}

function moveVideo(idx: number, direction: -1 | 1) {
  const nextIndex = idx + direction
  if (nextIndex < 0 || nextIndex >= props.modelValue.length) return
  const videos = [...props.modelValue]
  const [video] = videos.splice(idx, 1)
  videos.splice(nextIndex, 0, video)
  emit('update:modelValue', videos)
}

function updateVideo(idx: number, patch: Partial<CourseVideo>) {
  const videos = props.modelValue.map((video, index) =>
    index === idx ? { ...video, ...patch } : video,
  )
  emit('update:modelValue', videos)
}

function beforeVideoUpload(file: UploadRawFile) {
  const error = validateAdminVideoFile(file)
  if (error) {
    ElMessage.error(error)
    return false
  }
  return true
}

function createVideoUploadHandler(idx: number) {
  return (options: UploadRequestOptions) => handleVideoUpload(options, idx)
}

async function handleVideoUpload(options: UploadRequestOptions, idx: number) {
  const file = options.file as UploadRawFile
  try {
    const result = await uploadAdminVideo(file, 'course')
    updateVideo(idx, {
      url: result.url,
      title: props.modelValue[idx].title.trim() || `视频 ${idx + 1}`,
    })
    ElMessage.success('视频上传成功')
  } catch (error) {
    const message = error instanceof Error ? error.message : '视频上传失败'
    ElMessage.error(message)
  }
}

function openVideo(url: string) {
  if (!url) return
  window.open(url, '_blank', 'noopener,noreferrer')
}
</script>

<style scoped>
.video-edit-card { border: 1px solid #ebeef5; border-radius: 8px; padding: 16px; margin-bottom: 12px; }
.video-header-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
.video-actions { display: flex; gap: 4px; align-items: center; }
.video-label { font-weight: 500; }
.video-upload { margin-left: 120px; }
.upload-dragger-content { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 12px; }
.upload-icon { margin-bottom: 6px; font-size: 24px; color: #c0c4cc; }
.upload-text { font-size: 13px; color: #909399; }
</style>
