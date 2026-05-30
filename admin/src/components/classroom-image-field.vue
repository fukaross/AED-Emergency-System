<template>
  <div class="image-field">
    <div class="image-input-row">
      <el-input
        :model-value="modelValue"
        :placeholder="placeholder"
        clearable
        @update:model-value="emitValue"
      />
      <el-upload
        :show-file-list="false"
        :http-request="handleUpload"
        :before-upload="beforeUpload"
        accept="image/*"
      >
        <el-button type="primary" plain size="small" :loading="uploading">本地上传</el-button>
      </el-upload>
    </div>
    <div v-if="modelValue" class="image-preview">
      <el-image
        :src="modelValue"
        :preview-src-list="[modelValue]"
        fit="cover"
        class="preview-image"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { UploadRawFile, UploadRequestOptions } from 'element-plus'

const MAX_IMAGE_SIZE_MB = 5
const BYTES_PER_MB = 1024 * 1024

const props = defineProps<{
  modelValue: string
  scene: 'course' | 'quick-action'
  placeholder?: string
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const uploading = ref(false)

function emitValue(value: string | number) {
  emit('update:modelValue', String(value))
}

function beforeUpload(file: UploadRawFile) {
  if (!file.type.startsWith('image/')) {
    ElMessage.error('只能上传图片文件')
    return false
  }
  if (file.size > MAX_IMAGE_SIZE_MB * BYTES_PER_MB) {
    ElMessage.error(`图片大小不能超过 ${MAX_IMAGE_SIZE_MB}MB`)
    return false
  }
  return true
}

async function handleUpload(options: UploadRequestOptions) {
  const file = options.file as UploadRawFile
  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('scene', props.scene)

    const response = await fetch('/api/upload/image', {
      method: 'POST',
      headers: { 'X-Aed-Admin-Key': localStorage.getItem('adminKey') || '' },
      body: formData,
    })
    const result = await response.json()
    if (!response.ok || result.code !== 0 || !result.data?.url) {
      throw new Error(result.message || '图片上传失败')
    }
    emit('update:modelValue', result.data.url)
    ElMessage.success('图片上传成功')
  } catch (error) {
    const message = error instanceof Error ? error.message : '图片上传失败'
    ElMessage.error(message)
  } finally {
    uploading.value = false
  }
}
</script>

<style scoped>
.image-field {
  width: 100%;
}

.image-input-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.image-preview {
  margin-top: 8px;
}

.preview-image {
  width: 144px;
  height: 88px;
  border-radius: 4px;
  border: 1px solid #ebeef5;
}
</style>
