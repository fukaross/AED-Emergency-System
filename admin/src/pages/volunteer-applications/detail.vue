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

      <el-dialog v-model="approveDialogVisible" title="审核通过" width="520px">
        <div class="approve-help">
          坐标格式按地图常见复制顺序填写：经度 lng 在前、纬度 lat 在后，例如 {{ campusPointExample }}
        </div>
        <label class="approve-label">
          <span>校区</span>
          <el-select v-model="approveForm.campus" class="approve-field" @change="handleCampusChange">
            <el-option
              v-for="campus in siteConfig.campuses"
              :key="campus.code"
              :label="campus.name"
              :value="campus.code"
            />
          </el-select>
        </label>
        <div class="coordinate-grid">
          <label class="approve-label">
            <span>经度 lng（例：{{ defaultCampusPoint.longitude }}）</span>
            <el-input-number
              v-model="approveForm.lng"
              class="approve-field"
              :min="-180"
              :max="180"
              :precision="6"
              placeholder="先填经度"
            />
          </label>
          <label class="approve-label">
            <span>纬度 lat（例：{{ defaultCampusPoint.latitude }}）</span>
            <el-input-number
              v-model="approveForm.lat"
              class="approve-field"
              :min="-90"
              :max="90"
              :precision="6"
              placeholder="后填纬度"
            />
          </label>
        </div>
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
import { computed, reactive, ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
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
import { DEFAULT_CAMPUS, findCampus, siteConfig } from '@/config/site-config'

/** 审核通过弹窗的默认坐标来自配置的第一个校区。 */
const defaultCampusPoint = DEFAULT_CAMPUS.center
const campusPointExample = `${defaultCampusPoint.longitude},${defaultCampusPoint.latitude}`

interface ApproveForm {
  campus: string
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
const approveDialogVisible = ref(false)
const approveForm = reactive<ApproveForm>({
  campus: DEFAULT_CAMPUS.code,
  lat: defaultCampusPoint.latitude,
  lng: defaultCampusPoint.longitude,
  reviewNote: '',
})
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
  resetApproveForm()
  approveDialogVisible.value = true
}

async function submitApprove() {
  try {
    actionLoading.value = true
    const res = await approveVolunteerApplication(id, {
      campus: approveForm.campus,
      lat: approveForm.lat,
      lng: approveForm.lng,
      reviewNote: toNullableReviewNote(approveForm.reviewNote),
    })
    detail.value = res.data.data.application
    approveDialogVisible.value = false
    ElMessage.success(`已创建急救员草稿 ${res.data.data.createdRescuerId}`)
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '审核通过失败'))
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
function handleCampusChange(value: string) {
  const campus = findCampus(value)
  if (!campus) {
    // 下拉只列出配置中的校区；外部带入未知 code 时明确提示，不沿用上一个校区的坐标。
    ElMessage.error(`未知校区「${value}」，请检查 config/site.json`)
    return
  }
  approveForm.lat = campus.center.latitude
  approveForm.lng = campus.center.longitude
}
function resetApproveForm() {
  approveForm.campus = DEFAULT_CAMPUS.code
  approveForm.lat = defaultCampusPoint.latitude
  approveForm.lng = defaultCampusPoint.longitude
  approveForm.reviewNote = ''
}
function toNullableReviewNote(value: string) {
  const trimmed = value.trim()
  return trimmed ? trimmed : null
}

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
.approve-help { margin-bottom: 12px; color: #606266; font-size: 13px; }
.approve-label { display: block; margin-bottom: 12px; color: #303133; font-size: 13px; font-weight: 500; }
.approve-label span { display: block; margin-bottom: 6px; }
.approve-field { width: 100%; }
.coordinate-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
</style>
