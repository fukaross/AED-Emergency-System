<template>
  <admin-layout>
    <div class="page-content-padding">
      <el-button text class="back-link" @click="router.push('/responder-claims')">返回列表</el-button>
      <div v-loading="loading" class="detail-container">
        <template v-if="detail">
          <div class="admin-card">
            <div class="card-header-bar">
              <h3 class="card-header-title">认领申请</h3>
              <status-tag :status="detail.status" category="identityApplication" />
            </div>
            <el-descriptions :column="2" border>
              <el-descriptions-item label="申请ID">{{ detail.applicationId }}</el-descriptions-item>
              <el-descriptions-item label="OpenID">{{ detail.openId }}</el-descriptions-item>
              <el-descriptions-item label="姓名">{{ detail.payload.name }}</el-descriptions-item>
              <el-descriptions-item label="手机号">{{ detail.payload.phone }}</el-descriptions-item>
              <el-descriptions-item label="证件后 6 位">{{ detail.payload.idNumberLast6 }}</el-descriptions-item>
              <el-descriptions-item label="证书号">{{ detail.payload.certificateNo || '-' }}</el-descriptions-item>
              <el-descriptions-item label="绑定档案">{{ detail.responderProfileId || '-' }}</el-descriptions-item>
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
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { h, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElInput, ElMessage, ElMessageBox, ElOption, ElSelect } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getRescuers } from '@/api/rescuers'
import { approveResponderClaim, getResponderClaimDetail, rejectResponderClaim } from '@/api/user-identity'
import { getApiErrorMessage } from '@/api/client'
import type { AdminRescuerItem } from '@/types/rescuer'
import type { AdminResponderClaimDetail } from '@/types/user-identity'

const route = useRoute()
const router = useRouter()
const id = route.params.id as string
const detail = ref<AdminResponderClaimDetail | null>(null)
const candidates = ref<AdminRescuerItem[]>([])
const loading = ref(false)
const actionLoading = ref(false)

async function loadDetail() {
  loading.value = true
  try {
    const res = await getResponderClaimDetail(id)
    detail.value = res.data.data
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '加载认领详情失败'))
  } finally {
    loading.value = false
  }
}

async function loadCandidates() {
  const res = await getRescuers({ page: 1, pageSize: 50, keyword: detail.value?.payload.name || undefined })
  candidates.value = res.data.data.items
}

async function handleApprove() {
  await loadCandidates()
  const form = { responderProfileId: '', reviewNote: '' }
  const vnode = h('div', [
    h(ElSelect, { modelValue: form.responderProfileId, 'onUpdate:modelValue': (value: string) => { form.responderProfileId = value }, placeholder: '选择急救员档案', style: 'width: 100%; margin-bottom: 12px' }, () =>
      candidates.value.map((item) => h(ElOption, { label: `${item.name} · ${item.id}`, value: item.id }))),
    h(ElInput, { modelValue: form.reviewNote, 'onUpdate:modelValue': (value: string) => { form.reviewNote = value }, type: 'textarea', rows: 2, placeholder: '审核意见（可选）' }),
  ])
  try {
    await ElMessageBox.confirm(vnode, '审核通过', { confirmButtonText: '通过', cancelButtonText: '取消' })
    actionLoading.value = true
    const res = await approveResponderClaim(id, { responderProfileId: form.responderProfileId, reviewNote: form.reviewNote || null })
    detail.value = res.data.data.application
    ElMessage.success('已通过')
  } catch (error) {
    if (error !== 'cancel') ElMessage.error(getApiErrorMessage(error, '审核失败'))
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
    const res = await rejectResponderClaim(id, { reviewNote: result.value.trim() })
    detail.value = res.data.data.application
    ElMessage.success('已驳回')
  } catch (error) {
    if (error !== 'cancel') ElMessage.error(getApiErrorMessage(error, '驳回失败'))
  } finally {
    actionLoading.value = false
  }
}

onMounted(loadDetail)
</script>

<style scoped>
.back-link { margin-bottom: 16px; }
.detail-container { display: flex; flex-direction: column; gap: 16px; }
.action-bar { display: flex; gap: 12px; padding-bottom: 16px; }
</style>
