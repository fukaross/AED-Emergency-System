<template>
  <admin-layout>
    <div class="page-content-padding">
      <el-button text class="back-link" @click="router.push('/school-bindings')">返回列表</el-button>
      <div v-loading="loading" class="detail-container">
        <template v-if="detail">
          <div class="admin-card">
            <div class="card-header-bar">
              <h3 class="card-header-title">绑定申请</h3>
              <status-tag :status="detail.status" category="identityApplication" />
            </div>
            <el-descriptions :column="2" border>
              <el-descriptions-item label="申请ID">{{ detail.applicationId }}</el-descriptions-item>
              <el-descriptions-item label="OpenID">{{ detail.openId }}</el-descriptions-item>
              <el-descriptions-item label="姓名">{{ detail.payload.name }}</el-descriptions-item>
              <el-descriptions-item label="身份">{{ personTypeLabel(detail.payload.personType) }}</el-descriptions-item>
              <el-descriptions-item label="学院">{{ detail.payload.college }}</el-descriptions-item>
              <el-descriptions-item label="班级">{{ detail.payload.className || '-' }}</el-descriptions-item>
              <el-descriptions-item label="学号">{{ detail.payload.studentId || '-' }}</el-descriptions-item>
              <el-descriptions-item label="工号">{{ detail.payload.staffId || '-' }}</el-descriptions-item>
              <el-descriptions-item label="手机号">{{ detail.payload.phone || '-' }}</el-descriptions-item>
              <el-descriptions-item label="审核意见">{{ detail.reviewNote || '-' }}</el-descriptions-item>
            </el-descriptions>
          </div>
          <div v-if="detail.status === 'submitted'" class="action-bar">
            <el-button type="primary" :loading="actionLoading" @click="handleApprove">审核通过</el-button>
            <el-button type="danger" plain :loading="actionLoading" @click="handleReject">驳回申请</el-button>
          </div>
        </template>
        <el-empty v-else-if="!loading" description="申请不存在" />
      </div>
      <el-dialog v-model="approveDialogVisible" title="审核通过" width="520px">
        <el-radio-group v-model="approveForm.mode" class="approve-field">
          <el-radio value="new">创建新档案</el-radio>
          <el-radio value="existing">选择已有档案</el-radio>
        </el-radio-group>
        <el-select
          v-model="approveForm.schoolProfileId"
          class="approve-field"
          :disabled="approveForm.mode !== 'existing'"
          placeholder="选择学校档案"
        >
          <el-option
            v-for="item in profiles"
            :key="item.profileId"
            :label="`${item.name} · ${item.college}`"
            :value="String(item.profileId)"
          />
        </el-select>
        <el-input
          v-model="approveForm.reviewNote"
          type="textarea"
          :rows="2"
          placeholder="审核意见（可选）"
        />
        <template #footer>
          <el-button @click="approveDialogVisible = false">取消</el-button>
          <el-button type="primary" :loading="actionLoading" @click="submitApprove">通过</el-button>
        </template>
      </el-dialog>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { approveSchoolBinding, getSchoolBindingDetail, getSchoolProfiles, rejectSchoolBinding } from '@/api/user-identity'
import { getApiErrorMessage } from '@/api/client'
import type { AdminSchoolBindingDetail, AdminSchoolProfileItem, SchoolPersonType } from '@/types/user-identity'

type ApproveMode = 'new' | 'existing'

interface ApproveForm {
  mode: ApproveMode
  schoolProfileId: string
  reviewNote: string
}

const route = useRoute()
const router = useRouter()
const id = route.params.id as string
const detail = ref<AdminSchoolBindingDetail | null>(null)
const profiles = ref<AdminSchoolProfileItem[]>([])
const loading = ref(false)
const actionLoading = ref(false)
const approveDialogVisible = ref(false)
const approveForm = reactive<ApproveForm>({
  mode: 'new',
  schoolProfileId: '',
  reviewNote: '',
})

async function loadDetail() {
  loading.value = true
  try {
    const res = await getSchoolBindingDetail(id)
    detail.value = res.data.data
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '加载申请详情失败'))
  } finally {
    loading.value = false
  }
}

async function loadProfiles() {
  const keyword = detail.value?.payload.name || undefined
  const res = await getSchoolProfiles({ page: 1, pageSize: 50, keyword })
  profiles.value = res.data.data.items
}

async function handleApprove() {
  if (!detail.value) return
  try {
    await loadProfiles()
    resetApproveForm()
    approveDialogVisible.value = true
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '加载学校档案失败'))
  }
}

async function submitApprove() {
  if (!detail.value) return
  if (approveForm.mode === 'existing' && !approveForm.schoolProfileId) {
    ElMessage.error('请选择学校档案')
    return
  }
  try {
    actionLoading.value = true
    const reviewNote = toNullableReviewNote(approveForm.reviewNote)
    const data = approveForm.mode === 'existing'
      ? { schoolProfileId: Number(approveForm.schoolProfileId), reviewNote }
      : { newProfile: toNewProfile(detail.value), reviewNote }
    const res = await approveSchoolBinding(id, data)
    detail.value = res.data.data.application
    approveDialogVisible.value = false
    ElMessage.success('已通过')
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '审核失败'))
  } finally {
    actionLoading.value = false
  }
}

async function handleReject() {
  try {
    const result = await ElMessageBox.prompt('请输入审核意见', '驳回申请', {
      confirmButtonText: '驳回',
      cancelButtonText: '取消',
      inputType: 'textarea',
      inputValidator: (value) => value.trim().length > 0 && value.trim().length <= 200,
      inputErrorMessage: '审核意见必须为 1-200 字',
    })
    actionLoading.value = true
    const res = await rejectSchoolBinding(id, { reviewNote: result.value.trim() })
    detail.value = res.data.data.application
    ElMessage.success('已驳回')
  } catch (error) {
    if (error !== 'cancel') ElMessage.error(getApiErrorMessage(error, '驳回失败'))
  } finally {
    actionLoading.value = false
  }
}

function toNewProfile(item: AdminSchoolBindingDetail) {
  const payload = item.payload
  return { ...payload, status: 'active' as const }
}

function resetApproveForm() {
  approveForm.mode = 'new'
  approveForm.schoolProfileId = ''
  approveForm.reviewNote = ''
}

function toNullableReviewNote(value: string) {
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : null
}

function personTypeLabel(value: SchoolPersonType) {
  return value === 'teacher' ? '教师' : value === 'staff' ? '职工' : '学生'
}

onMounted(loadDetail)
</script>

<style scoped>
.back-link { margin-bottom: 16px; }
.detail-container { display: flex; flex-direction: column; gap: 16px; }
.action-bar { display: flex; gap: 12px; padding-bottom: 16px; }
.approve-field { width: 100%; margin-bottom: 12px; }
</style>
