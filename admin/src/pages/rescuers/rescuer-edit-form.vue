<template>
  <div>
    <div class="admin-card">
      <div class="card-header-bar">
        <h3 class="card-header-title">基本信息</h3>
      </div>
      <div class="profile-header">
        <el-avatar :src="modelValue.avatarUrl" :size="64" class="profile-avatar" />
        <div class="profile-id">
          <span class="id-label">编号</span>
          <span class="id-value">{{ rescuerId }}</span>
        </div>
      </div>
      <el-form :model="modelValue" label-width="100px" class="detail-form">
        <el-form-item label="头像">
          <el-upload
            class="avatar-upload"
            drag
            :show-file-list="false"
            :http-request="handleUpload"
            :before-upload="beforeUpload"
            accept="image/*"
          >
            <div class="upload-dragger-content">
              <el-icon class="upload-icon"><UploadFilled /></el-icon>
              <div class="upload-text">将图片拖到此处或点击上传</div>
            </div>
          </el-upload>
          <el-input
            :model-value="modelValue.avatarUrl"
            placeholder="或直接输入图片URL"
            class="avatar-url-input"
            @update:model-value="set('avatarUrl', $event)"
          />
        </el-form-item>
        <el-form-item label="姓名">
          <el-input :model-value="modelValue.name" @update:model-value="set('name', $event)" />
        </el-form-item>
        <el-form-item label="电话">
          <el-input :model-value="modelValue.phone" placeholder="手机号" @update:model-value="set('phone', $event)" />
        </el-form-item>
        <el-form-item label="身份证号">
          <el-input
            :model-value="modelValue.idNumber"
            placeholder="18位身份证号"
            maxlength="18"
            @update:model-value="set('idNumber', $event)"
            @blur="validateIdNumberLocal"
          />
          <div v-if="idNumberError" class="field-error">{{ idNumberError }}</div>
        </el-form-item>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="校区">
              <el-select :model-value="modelValue.campus" style="width: 100%" @update:model-value="set('campus', $event)">
                <el-option label="REDACTED" value="guilinyang" />
                <el-option label="府城" value="fucheng" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="等级">
              <el-select :model-value="modelValue.level" style="width: 100%" clearable @update:model-value="set('level', $event)">
                <el-option label="荣誉急救员" value="honor" />
                <el-option label="预备急救员" value="trainee" />
                <el-option label="正式急救员" value="official" />
                <el-option label="骨干急救员" value="core" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="资格状态">
              <el-select :model-value="modelValue.qualificationStatus" style="width: 100%" @update:model-value="set('qualificationStatus', $event)">
                <el-option label="已激活" value="active" />
                <el-option label="未激活" value="inactive" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="坐标">
              <el-input
                :model-value="`${modelValue.lat.toFixed(5)}, ${modelValue.lng.toFixed(5)}`"
                placeholder="lat, lng"
                @change="handleCoordChange"
              />
              <div class="field-hint">当前为固定坐标（离线数据），后续版本将支持实时 GPS 定位采集</div>
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
    </div>

    <div class="admin-card">
      <div class="card-header-bar">
        <h3 class="card-header-title">证书与技能</h3>
      </div>
      <el-form :model="modelValue" label-width="100px" class="detail-form">
        <el-form-item label="证书号">
          <el-input :model-value="modelValue.certificateNo" placeholder="完整证书编号" @update:model-value="set('certificateNo', $event)" />
        </el-form-item>
        <el-form-item label="脱敏证书号">
          <el-input :model-value="certificateNoMasked" disabled />
        </el-form-item>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="发证单位">
              <el-input :model-value="modelValue.issuingAuthority" @update:model-value="set('issuingAuthority', $event)" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="发证日期">
              <el-input :model-value="modelValue.issueDate" @update:model-value="set('issueDate', $event)" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="工作单位">
          <el-input :model-value="modelValue.workUnit" @update:model-value="set('workUnit', $event)" />
        </el-form-item>
        <el-form-item label="技能">
          <el-checkbox :model-value="modelValue.skillCpr" @update:model-value="set('skillCpr', $event)">CPR</el-checkbox>
          <el-checkbox :model-value="modelValue.skillAed" style="margin-left: 16px" @update:model-value="set('skillAed', $event)">AED</el-checkbox>
          <el-checkbox :model-value="modelValue.skillStb" style="margin-left: 16px" @update:model-value="set('skillStb', $event)">止血包扎</el-checkbox>
        </el-form-item>
      </el-form>
    </div>

    <rescuer-assessment-form
      :model-value="modelValue"
      @update:model-value="emit('update:modelValue', $event)"
    />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { UploadFilled } from '@element-plus/icons-vue'
import type { UploadRequestOptions, UploadRawFile } from 'element-plus'
import type { RescuerForm } from './use-rescuer-form'
import rescuerAssessmentForm from './rescuer-assessment-form.vue'
import { validateIdNumber } from '@/utils/id-number'

const props = defineProps<{
  modelValue: RescuerForm
  rescuerId: string
  certificateNoMasked: string | null
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: RescuerForm): void
}>()

const idNumberError = ref('')

function set<K extends keyof RescuerForm>(key: K, value: RescuerForm[K]) {
  if (key === 'idNumber') idNumberError.value = ''
  emit('update:modelValue', { ...props.modelValue, [key]: value })
}

function handleCoordChange(value: string) {
  const parts = value.split(',').map(s => s.trim())
  const lat = parseFloat(parts[0])
  const lng = parseFloat(parts[1])
  if (!isNaN(lat) && !isNaN(lng)) {
    emit('update:modelValue', { ...props.modelValue, lat, lng })
  }
}

function validateIdNumberLocal() {
  idNumberError.value = validateIdNumber(props.modelValue.idNumber)
}

async function handleUpload(options: UploadRequestOptions) {
  const file = options.file as UploadRawFile
  try {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('scene', 'rescuer')

    const response = await fetch('/api/upload/image', {
      method: 'POST',
      headers: { 'X-Aed-Admin-Key': localStorage.getItem('adminKey') || '' },
      body: formData,
    })
    const result = await response.json()
    if (result.code === 0 && result.data?.url) {
      set('avatarUrl', result.data.url)
      ElMessage.success('头像上传成功')
    } else {
      ElMessage.error(result.message || '头像上传失败')
    }
  } catch {
    ElMessage.error('头像上传失败，请检查网络或手动输入 URL')
  }
}

function beforeUpload(file: UploadRawFile) {
  const isImage = file.type.startsWith('image/')
  if (!isImage) { ElMessage.error('只能上传图片文件'); return false }
  const isLt5M = file.size / 1024 / 1024 < 5
  if (!isLt5M) { ElMessage.error('图片大小不能超过 5MB'); return false }
  return true
}
</script>

<style scoped>
.profile-header {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 0 0 16px;
  margin-bottom: 8px;
  border-bottom: 1px solid #ebeef5;
}

.profile-avatar {
  flex-shrink: 0;
}

.profile-id {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.id-label {
  font-size: 12px;
  color: #909399;
}

.id-value {
  font-size: 14px;
  font-weight: 500;
  color: #303133;
}

.detail-form {
  max-width: 720px;
}

.field-hint {
  font-size: 12px;
  color: #909399;
  margin-top: 2px;
}

.field-error {
  font-size: 12px;
  color: #f56c6c;
  margin-top: 2px;
}

.avatar-upload {
  margin-bottom: 8px;
}

.avatar-url-input {
  margin-top: 4px;
}

.upload-dragger-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 16px;
}

.upload-icon {
  font-size: 28px;
  color: #c0c4cc;
  margin-bottom: 8px;
}

.upload-text {
  font-size: 13px;
  color: #909399;
}
</style>
