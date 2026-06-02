<template>
  <admin-layout>
    <div class="page-content-padding">
      <el-button text class="back-link" @click="goBack">
        <el-icon><ArrowLeft /></el-icon>
        <span>返回列表</span>
      </el-button>

      <div v-loading="loading" class="detail-container">
        <template v-if="detail">
          <div class="admin-card">
            <div class="card-header-bar">
              <h3 class="card-header-title">申请信息</h3>
              <status-tag :status="detail.status" category="volunteerApplication" />
            </div>
            <el-descriptions :column="2" border>
              <el-descriptions-item label="申请ID">{{ detail.applicationId }}</el-descriptions-item>
              <el-descriptions-item label="OpenID">{{ detail.openId }}</el-descriptions-item>
              <el-descriptions-item label="申请轮次">{{ detail.attemptNumber }}</el-descriptions-item>
              <el-descriptions-item label="提交时间">{{ formatTime(detail.submittedAt) }}</el-descriptions-item>
              <el-descriptions-item label="审核人">{{ detail.reviewedBy || '-' }}</el-descriptions-item>
              <el-descriptions-item label="审核时间">{{ detail.reviewedAt ? formatTime(detail.reviewedAt) : '-' }}</el-descriptions-item>
              <el-descriptions-item label="急救员草稿">
                <el-button
                  v-if="detail.createdRescuerId"
                  text
                  type="primary"
                  @click="goRescuer(detail.createdRescuerId)"
                >
                  {{ detail.createdRescuerId }}
                </el-button>
                <span v-else>-</span>
              </el-descriptions-item>
              <el-descriptions-item label="审核意见">{{ detail.reviewNote || '-' }}</el-descriptions-item>
            </el-descriptions>
          </div>

          <div class="admin-card">
            <div class="card-header-bar">
              <h3 class="card-header-title">申请人材料</h3>
            </div>
            <el-descriptions :column="2" border>
              <el-descriptions-item label="姓名">{{ detail.payload.name }}</el-descriptions-item>
              <el-descriptions-item label="性别">{{ genderLabel(detail.payload.gender) }}</el-descriptions-item>
              <el-descriptions-item label="出生日期">{{ detail.payload.birthDate }}</el-descriptions-item>
              <el-descriptions-item label="手机号">{{ detail.payload.phone }}</el-descriptions-item>
              <el-descriptions-item label="邮箱">{{ detail.payload.email }}</el-descriptions-item>
              <el-descriptions-item label="身份证号">{{ detail.payload.idNumber }}</el-descriptions-item>
              <el-descriptions-item label="政治面貌">{{ detail.payload.politicalStatus }}</el-descriptions-item>
              <el-descriptions-item label="学历">{{ detail.payload.education }}</el-descriptions-item>
              <el-descriptions-item label="职业">{{ detail.payload.occupation }}</el-descriptions-item>
              <el-descriptions-item label="职称">{{ detail.payload.professionalTitle }}</el-descriptions-item>
              <el-descriptions-item label="紧急联系人">{{ detail.payload.emergencyContactName }}</el-descriptions-item>
              <el-descriptions-item label="紧急电话">{{ detail.payload.emergencyContactPhone }}</el-descriptions-item>
              <el-descriptions-item label="住址" :span="2">{{ detail.payload.homeAddress }}</el-descriptions-item>
              <el-descriptions-item label="个人简历" :span="2">{{ detail.payload.personalResume }}</el-descriptions-item>
              <el-descriptions-item label="加入原因" :span="2">{{ detail.payload.joinReason }}</el-descriptions-item>
            </el-descriptions>
          </div>

          <div class="admin-card">
            <div class="card-header-bar">
              <h3 class="card-header-title">附件</h3>
            </div>
            <div class="image-section">
              <div
                v-for="group in imageGroups"
                :key="group.title"
                class="image-group"
              >
                <div class="image-title">{{ group.title }}</div>
                <div class="image-list">
                  <el-image
                    v-for="image in group.images"
                    :key="image.url"
                    :src="image.url"
                    :preview-src-list="group.images.map(item => item.url)"
                    fit="cover"
                    class="attachment-image"
                    preview-teleported
                  />
                </div>
              </div>
            </div>
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
import { computed, h, ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElInput, ElInputNumber, ElMessage, ElMessageBox, ElSelect, ElOption } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import {
  approveVolunteerApplication,
  getVolunteerApplicationDetail,
  rejectVolunteerApplication,
} from '@/api/volunteer-applications'
import { getApiErrorMessage } from '@/api/client'
import type { AdminVolunteerApplicationDetail } from '@/types/volunteer-application'

const CAMPUS_POINTS = {
  guilinyang: { label: 'REDACTED校区', lat: REDACTED, lng: REDACTED },
  fucheng: { label: '府城校区', lat: REDACTED, lng: REDACTED },
} as const
type CampusCode = keyof typeof CAMPUS_POINTS

interface ApproveForm {
  campus: CampusCode
  lat: number
  lng: number
  reviewNote: string
}

const route = useRoute()
const router = useRouter()
const id = route.params.id as string
const loading = ref(false)
const actionLoading = ref(false)
const detail = ref<AdminVolunteerApplicationDetail | null>(null)
const imageGroups = computed(() => {
  if (!detail.value) return []
  const payload = detail.value.payload
  return [
    { title: '证件照', images: [payload.idPhoto] },
    { title: '身份证正反面', images: [payload.idCardFront, payload.idCardBack] },
    { title: '资格证明', images: payload.qualificationImages },
    { title: '志愿者证书', images: payload.volunteerCertificateImages },
  ]
})

async function loadDetail() {
  loading.value = true
  try {
    const res = await getVolunteerApplicationDetail(id)
    detail.value = res.data.data
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '加载申请详情失败'))
  } finally {
    loading.value = false
  }
}

async function handleApprove() {
  const form: ApproveForm = {
    campus: 'guilinyang',
    lat: CAMPUS_POINTS.guilinyang.lat,
    lng: CAMPUS_POINTS.guilinyang.lng,
    reviewNote: '',
  }
  const vnode = h('div', { class: 'approve-form' }, [
    h(ElSelect, {
      modelValue: form.campus,
      'onUpdate:modelValue': (value: CampusCode) => {
        form.campus = value
        form.lat = CAMPUS_POINTS[value].lat
        form.lng = CAMPUS_POINTS[value].lng
      },
      style: 'width: 100%; margin-bottom: 12px',
    }, () => Object.entries(CAMPUS_POINTS).map(([value, item]) =>
      h(ElOption, { label: item.label, value }))),
    h(ElInputNumber, {
      modelValue: form.lat,
      'onUpdate:modelValue': (value: number | undefined) => { if (value !== undefined) form.lat = value },
      min: -90,
      max: 90,
      precision: 6,
      style: 'width: 100%; margin-bottom: 12px',
    }),
    h(ElInputNumber, {
      modelValue: form.lng,
      'onUpdate:modelValue': (value: number | undefined) => { if (value !== undefined) form.lng = value },
      min: -180,
      max: 180,
      precision: 6,
      style: 'width: 100%; margin-bottom: 12px',
    }),
    h(ElInput, {
      modelValue: form.reviewNote,
      'onUpdate:modelValue': (value: string) => { form.reviewNote = value },
      type: 'textarea',
      rows: 2,
      placeholder: '审核意见（可选）',
    }),
  ])
  try {
    await ElMessageBox.confirm(vnode, '审核通过', { confirmButtonText: '通过', cancelButtonText: '取消' })
    actionLoading.value = true
    const res = await approveVolunteerApplication(id, {
      campus: form.campus,
      lat: form.lat,
      lng: form.lng,
      reviewNote: form.reviewNote || null,
    })
    detail.value = res.data.data.application
    ElMessage.success(`已创建急救员草稿 ${res.data.data.createdRescuerId}`)
  } catch (error) {
    if (error !== 'cancel') ElMessage.error(getApiErrorMessage(error, '审核通过失败'))
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
    const res = await rejectVolunteerApplication(id, { reviewNote: result.value.trim() })
    detail.value = res.data.data.application
    ElMessage.success('已驳回')
  } catch (error) {
    if (error !== 'cancel') ElMessage.error(getApiErrorMessage(error, '驳回失败'))
  } finally {
    actionLoading.value = false
  }
}

function goBack() { router.push('/volunteer-applications') }
function goRescuer(rescuerId: string) { router.push(`/rescuers/${rescuerId}`) }
function formatTime(value: string) { return new Date(value).toLocaleString('zh-CN', { hour12: false }) }
function genderLabel(value: string) { return value === 'female' ? '女' : value === 'male' ? '男' : '未知' }

onMounted(loadDetail)
</script>

<style scoped>
.back-link { margin-bottom: 16px; }
.detail-container { display: flex; flex-direction: column; gap: 16px; }
.image-section { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; }
.image-title { font-size: 14px; color: #606266; margin-bottom: 8px; }
.image-list { display: flex; flex-wrap: wrap; gap: 8px; }
.attachment-image { width: 96px; height: 96px; border-radius: 4px; border: 1px solid #ebeef5; }
.action-bar { display: flex; gap: 12px; padding-bottom: 16px; }
</style>
