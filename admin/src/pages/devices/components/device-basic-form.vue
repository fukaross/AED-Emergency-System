<template>
  <!-- eslint-disable vue/no-mutating-props -->
  <el-form
    ref="formRef"
    :model="form"
    :rules="formRules"
    label-width="100px"
    label-position="top"
    class="device-form"
  >
    <el-row :gutter="24">
      <el-col :xs="24" :sm="12">
        <el-form-item label="设备编号">
          <el-input :model-value="form.id || '自动生成'" disabled />
        </el-form-item>
      </el-col>
      <el-col :xs="24" :sm="12">
        <el-form-item label="设备名称" prop="name">
          <el-input v-model="form.name" placeholder="如：REDACTED" />
        </el-form-item>
      </el-col>
    </el-row>

    <el-form-item label="地图选点">
      <map-picker
        :lat="form.lat"
        :lng="form.lng"
        @update:lat="(v) => (form.lat = v)"
        @update:lng="(v) => (form.lng = v)"
        @update:address="(v) => (form.address = v)"
      />
    </el-form-item>

    <el-row :gutter="24">
      <el-col :xs="24" :sm="12">
        <el-form-item label="纬度" prop="lat">
          <el-input-number
            :model-value="form.lat"
            :precision="6"
            :step="0.0001"
            :min="-90"
            :max="90"
            controls-position="right"
            style="width: 100%"
            @change="($event: number | undefined) => (form.lat = $event ?? 0)"
          />
        </el-form-item>
      </el-col>
      <el-col :xs="24" :sm="12">
        <el-form-item label="经度" prop="lng">
          <el-input-number
            :model-value="form.lng"
            :precision="6"
            :step="0.0001"
            :min="-180"
            :max="180"
            controls-position="right"
            style="width: 100%"
            @change="($event: number | undefined) => (form.lng = $event ?? 0)"
          />
        </el-form-item>
      </el-col>
    </el-row>

    <el-form-item label="详细地址">
      <el-input v-model="form.address" placeholder="如：REDACTEDREDACTED" />
    </el-form-item>

    <el-form-item label="设备图片">
      <div class="image-field">
        <el-input v-model="form.image" placeholder="图片 URL 或上传图片" />
        <el-upload
          class="image-upload"
          :show-file-list="false"
          :http-request="onUpload"
          :before-upload="beforeUpload"
          accept="image/*"
        >
          <el-button type="primary" plain size="small">本地上传</el-button>
        </el-upload>
      </div>
      <div v-if="form.image" class="image-preview">
        <el-image
          :src="form.image"
          :preview-src-list="[form.image]"
          fit="cover"
          style="width: 120px; height: 80px; border-radius: 4px; border: 1px solid #ebeef5"
        />
      </div>
    </el-form-item>

    <el-row :gutter="24">
      <el-col :xs="24" :sm="12">
        <el-form-item label="上次维护日期">
          <el-input v-model="form.lastMaintenance" placeholder="如：2026-03-15" />
        </el-form-item>
      </el-col>
      <el-col :xs="24" :sm="12">
        <el-form-item label="下次维护日期">
          <el-input v-model="form.nextMaintenance" placeholder="如：2026-09-15" />
        </el-form-item>
      </el-col>
    </el-row>

    <el-form-item label="维护人员">
      <el-input v-model="form.maintainer" placeholder="如：张三" />
    </el-form-item>
  </el-form>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { FormInstance, FormRules, UploadRequestOptions, UploadRawFile } from 'element-plus'
import { ElMessage } from 'element-plus'
import mapPicker from '@/components/map-picker.vue'
import type { DeviceForm } from '../use-device-detail'

defineProps<{
  form: DeviceForm
  formRules: FormRules
  onUpload: (options: UploadRequestOptions) => void
}>()

const formRef = ref<FormInstance>()

function beforeUpload(file: UploadRawFile) {
  const isImage = file.type.startsWith('image/')
  if (!isImage) {
    ElMessage.error('只能上传图片文件')
    return false
  }
  const isLt5M = file.size / 1024 / 1024 < 5
  if (!isLt5M) {
    ElMessage.error('图片大小不能超过 5MB')
    return false
  }
  return true
}

defineExpose({ validate: () => formRef.value?.validate() })
</script>

<style scoped>
.device-form {
  max-width: 800px;
}

.image-field {
  display: flex;
  gap: 8px;
  align-items: center;
  width: 100%;
}

.image-field .el-input {
  flex: 1;
}

.image-upload {
  flex-shrink: 0;
}

.image-preview {
  margin-top: 8px;
}
</style>
