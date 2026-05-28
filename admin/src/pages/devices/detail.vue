<template>
  <admin-layout>
    <div class="page-content-padding">
      <el-button text class="back-link" @click="goBack">
        <el-icon><ArrowLeft /></el-icon>
        <span>返回列表</span>
      </el-button>

      <div v-loading="loading" class="detail-container">
        <div class="admin-card">
          <div class="card-header-bar">
            <h3 class="card-header-title">{{ isCreate ? '新增 AED 设备' : 'AED 设备详情' }}</h3>
          </div>

          <div class="form-body">
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

              <el-row :gutter="24">
                <el-col :xs="24" :sm="12">
                  <el-form-item label="纬度" prop="lat">
                    <el-input-number
                      v-model="form.lat"
                      :precision="6"
                      :step="0.0001"
                      :min="-90"
                      :max="90"
                      controls-position="right"
                      style="width: 100%"
                    />
                  </el-form-item>
                </el-col>
                <el-col :xs="24" :sm="12">
                  <el-form-item label="经度" prop="lng">
                    <el-input-number
                      v-model="form.lng"
                      :precision="6"
                      :step="0.0001"
                      :min="-180"
                      :max="180"
                      controls-position="right"
                      style="width: 100%"
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
                    :http-request="handleUpload"
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
          </div>
        </div>

        <div v-if="!isCreate" class="admin-card">
          <div class="card-header-bar">
            <h3 class="card-header-title">设备状态</h3>
          </div>

          <div class="status-section">
            <div class="status-row">
              <span class="status-label">当前状态：</span>
              <status-tag :status="currentStatus" category="device" />
            </div>

            <div v-if="currentStatus === 'online'" class="status-action">
              <el-button type="danger" plain @click="handleSetOffline">
                标记为离线
              </el-button>
            </div>
            <div v-else class="status-action">
              <el-button type="success" @click="handleSetOnline">
                恢复在线
              </el-button>
              <div v-if="form.statusNote" class="offline-note">
                <span class="note-label">离线原因：</span>
                <span class="note-text">{{ form.statusNote }}</span>
              </div>
            </div>
          </div>
        </div>

        <div class="action-bar">
          <el-button type="primary" :loading="saveLoading" @click="handleSave">
            {{ isCreate ? '创建设备' : '保存修改' }}
          </el-button>
          <el-button v-if="!isCreate" type="danger" plain :loading="deleteLoading" @click="handleDelete">
            删除设备
          </el-button>
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import type { FormInstance, FormRules, UploadRequestOptions, UploadRawFile } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getAedDeviceDetail, createAedDevice, updateAedDevice, deleteAedDevice } from '@/api/devices'
import { getApiErrorMessage } from '@/api/client'
import { createRequestSequencer } from '@/utils/request-sequencer'
import type { AdminAedDeviceItem } from '@/types/device'

const route = useRoute()
const router = useRouter()
const formRef = ref<FormInstance>()
const loading = ref(false)
const saveLoading = ref(false)
const deleteLoading = ref(false)
const formDirty = ref(false)
const sequencer = createRequestSequencer()

const deviceId = computed(() => route.params.id as string)
const isCreate = computed(() => deviceId.value === 'new')
const currentStatus = computed(() => (form.value.status as 'online' | 'offline') || 'online')

interface DeviceForm {
  id: string
  name: string
  lat: number
  lng: number
  image: string
  address: string
  status: string
  statusNote: string | null
  lastMaintenance: string
  nextMaintenance: string
  maintainer: string
}

const form = ref<DeviceForm>({
  id: '',
  name: '',
  lat: 0,
  lng: 0,
  image: '',
  address: '',
  status: 'online',
  statusNote: null,
  lastMaintenance: '',
  nextMaintenance: '',
  maintainer: '',
})

const originalForm = ref<DeviceForm | null>(null)

const formRules: FormRules = {
  name: [{ required: true, message: '请输入设备名称', trigger: 'blur' }],
  lat: [{ required: true, message: '请输入纬度', trigger: 'blur' }],
  lng: [{ required: true, message: '请输入经度', trigger: 'blur' }],
}

function populateForm(device: AdminAedDeviceItem) {
  const data: DeviceForm = {
    id: device.id,
    name: device.name,
    lat: device.lat,
    lng: device.lng,
    image: device.image,
    address: device.address,
    status: device.status,
    statusNote: device.statusNote,
    lastMaintenance: device.lastMaintenance,
    nextMaintenance: device.nextMaintenance,
    maintainer: device.maintainer,
  }
  form.value = { ...data }
  originalForm.value = { ...data }
  nextTick(() => {
    formDirty.value = false
  })
}

watch(
  () => form.value,
  () => {
    formDirty.value = true
  },
  { deep: true },
)

async function loadDetail() {
  if (isCreate.value) return
  if (formDirty.value) return
  loading.value = true
  const seq = sequencer.next()
  try {
    const res = await getAedDeviceDetail(deviceId.value)
    if (!sequencer.isCurrent(seq)) return
    populateForm(res.data.data)
  } catch (error) {
    if (!sequencer.isCurrent(seq)) return
    ElMessage.error(getApiErrorMessage(error, '加载设备详情失败'))
    router.replace('/devices')
  } finally {
    if (sequencer.isCurrent(seq)) {
      loading.value = false
    }
  }
}

async function handleSave() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  saveLoading.value = true
  try {
    if (isCreate.value) {
      await createAedDevice({
        name: form.value.name,
        lat: form.value.lat,
        lng: form.value.lng,
        image: form.value.image || undefined,
        address: form.value.address || undefined,
        status: form.value.status as 'online' | 'offline' | undefined,
        statusNote: form.value.statusNote || undefined,
        lastMaintenance: form.value.lastMaintenance || undefined,
        nextMaintenance: form.value.nextMaintenance || undefined,
        maintainer: form.value.maintainer || undefined,
      })
      ElMessage.success('设备创建成功')
      router.replace('/devices')
    } else {
      const payload: Record<string, unknown> = {}
      for (const key of Object.keys(form.value) as (keyof DeviceForm)[]) {
        if (key === 'id') continue
        const current = form.value[key]
        const original = originalForm.value?.[key]
        if (current !== original && key !== 'statusNote') {
          payload[key] = current
        }
        if (key === 'statusNote' && current !== original) {
          payload.statusNote = current === '' ? null : current
        }
      }
      if (Object.keys(payload).length === 0) {
        ElMessage.info('没有需要保存的更改')
        saveLoading.value = false
        return
      }
      const res = await updateAedDevice(deviceId.value, payload)
      ElMessage.success('设备信息已更新')
      populateForm(res.data.data)
    }
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '保存失败'))
  } finally {
    saveLoading.value = false
  }
}

async function handleSetOffline() {
  try {
    const { value } = await ElMessageBox.prompt('请输入离线原因（必填）', '标记为离线', {
      confirmButtonText: '确认离线',
      cancelButtonText: '取消',
      inputType: 'textarea',
      inputValidator: (val) => {
        if (!val || !val.trim()) return '离线原因不能为空'
        return true
      },
      inputErrorMessage: '离线原因不能为空',
    })

    if (!value || !value.trim()) return

    const res = await updateAedDevice(deviceId.value, {
      status: 'offline',
      statusNote: value.trim(),
    })
    ElMessage.success('设备已标记为离线')
    populateForm(res.data.data)
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getApiErrorMessage(error, '状态变更失败'))
    }
  }
}

async function handleSetOnline() {
  try {
    await ElMessageBox.confirm('确认恢复该设备为在线状态？', '恢复在线', {
      confirmButtonText: '确认恢复',
      cancelButtonText: '取消',
      type: 'info',
    })

    const res = await updateAedDevice(deviceId.value, {
      status: 'online',
      statusNote: null,
    })
    ElMessage.success('设备已恢复在线')
    populateForm(res.data.data)
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getApiErrorMessage(error, '状态变更失败'))
    }
  }
}

async function handleDelete() {
  try {
    await ElMessageBox.confirm(
      `确定删除设备 "${form.value.name}"？此操作不可撤销。`,
      '删除确认',
      {
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        type: 'warning',
      },
    )

    deleteLoading.value = true
    await deleteAedDevice(deviceId.value)
    ElMessage.success('设备已删除')
    router.replace('/devices')
  } catch (error) {
    if (error === 'cancel') return
    ElMessage.error(getApiErrorMessage(error, '删除失败'))
  } finally {
    deleteLoading.value = false
  }
}

async function handleUpload(options: UploadRequestOptions) {
  const file = options.file as UploadRawFile
  try {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('scene', 'device')

    const uploadUrl = '/api/upload/image'
    const response = await fetch(uploadUrl, {
      method: 'POST',
      headers: { 'X-Aed-Admin-Key': localStorage.getItem('adminKey') || '' },
      body: formData,
    })
    const result = await response.json()
    if (result.code === 0 && result.data?.url) {
      form.value.image = result.data.url
      ElMessage.success('图片上传成功')
    } else {
      ElMessage.error(result.message || '图片上传失败')
    }
  } catch {
    ElMessage.error('图片上传失败，请检查网络或手动输入 URL')
  }
}

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

function goBack() {
  router.back()
}

const pollTimer = ref<ReturnType<typeof setInterval> | null>(null)

function startPolling() {
  stopPolling()
  pollTimer.value = setInterval(loadDetail, 10000)
}

function stopPolling() {
  if (pollTimer.value) {
    clearInterval(pollTimer.value)
    pollTimer.value = null
  }
}

onMounted(() => {
  loadDetail()
  if (!isCreate.value) {
    startPolling()
  }
})

onUnmounted(() => {
  stopPolling()
})
</script>

<style scoped>
.back-link {
  margin-bottom: 16px;
  padding: 0;
  font-size: 14px;
  color: #606266;
}

.back-link .el-icon {
  margin-right: 4px;
}

.detail-container {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.form-body {
  padding: 16px 24px 24px;
}

@media (max-width: 767px) {
  .form-body {
    padding: 12px 16px 16px;
  }
}

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

.status-section {
  padding: 16px 24px 24px;
}

.status-row {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 16px;
}

.status-label {
  font-size: 14px;
  color: #606266;
}

.status-action {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.offline-note {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  padding: 10px 14px;
  background-color: #fef0f0;
  border-radius: 4px;
  border: 1px solid #fde2e2;
}

.note-label {
  font-size: 13px;
  color: #909399;
  flex-shrink: 0;
}

.note-text {
  font-size: 14px;
  color: #f56c6c;
}

.action-bar {
  display: flex;
  gap: 12px;
  padding-top: 4px;
}
</style>
