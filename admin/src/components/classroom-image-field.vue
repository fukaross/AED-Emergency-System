<template>
  <div class="image-field">
    <div class="image-input-row">
      <el-input
        :model-value="modelValue"
        :placeholder="placeholder"
        clearable
        @update:model-value="emitValue"
      />
    </div>
    <el-upload
      class="image-upload"
      drag
      :show-file-list="false"
      :http-request="handleUpload"
      :before-upload="beforeUpload"
      :disabled="uploading"
      accept="image/*"
    >
      <div class="upload-dragger-content">
        <el-icon class="upload-icon"><UploadFilled /></el-icon>
        <div class="upload-text">{{ uploading ? '上传中...' : '将图片拖到此处或点击上传' }}</div>
      </div>
    </el-upload>
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
import { UploadFilled } from '@element-plus/icons-vue'
import type { UploadRawFile, UploadRequestOptions } from 'element-plus'
import { uploadAdminImage, validateAdminImageFile } from '@/utils/admin-image-upload'

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
  const error = validateAdminImageFile(file)
  if (error) {
    ElMessage.error(error)
    return false
  }
  return true
}

async function handleUpload(options: UploadRequestOptions) {
  const file = options.file as UploadRawFile
  uploading.value = true
  try {
    const result = await uploadAdminImage(file, props.scene)
    emit('update:modelValue', result.url)
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
  width: 100%;
}

.image-upload {
  margin-top: 8px;
}

.upload-dragger-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 12px;
}

.upload-icon {
  margin-bottom: 6px;
  font-size: 24px;
  color: #c0c4cc;
}

.upload-text {
  font-size: 13px;
  color: #909399;
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
