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
            <device-basic-form
              ref="basicFormRef"
              :form="form"
              :form-rules="formRules"
              :on-upload="handleUpload"
            />
          </div>
        </div>

        <device-status-card
          v-if="!isCreate"
          :current-status="currentStatus"
          :status-note="form.statusNote"
          @set-offline="handleSetOffline"
          @set-online="handleSetOnline"
        />

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
import { ref, watch, onMounted, onUnmounted } from 'vue'
import { ArrowLeft } from '@element-plus/icons-vue'
import type { FormRules } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import deviceBasicForm from './components/device-basic-form.vue'
import deviceStatusCard from './components/device-status-card.vue'
import { useDeviceDetail } from './use-device-detail'

const basicFormRef = ref<InstanceType<typeof deviceBasicForm>>()

const {
  form,
  formDirty,
  loading,
  saveLoading,
  deleteLoading,
  isCreate,
  currentStatus,
  handleSave: baseHandleSave,
  handleSetOffline,
  handleSetOnline,
  handleDelete,
  handleUpload,
  loadDetail,
  startPolling,
  stopPolling,
  goBack,
} = useDeviceDetail()

async function handleSave() {
  const valid = await basicFormRef.value?.validate?.() ?? false
  await baseHandleSave(!!valid)
}

const formRules: FormRules = {
  name: [{ required: true, message: '请输入设备名称', trigger: 'blur' }],
  lat: [{ required: true, message: '请输入纬度', trigger: 'blur' }],
  lng: [{ required: true, message: '请输入经度', trigger: 'blur' }],
}

watch(form, () => {
  formDirty.value = true
}, { deep: true })

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

.action-bar {
  display: flex;
  gap: 12px;
  padding-top: 4px;
}
</style>
