<template>
  <admin-layout>
    <div class="detail-container">
      <!-- Back button header -->
      <div class="detail-header-action">
        <el-button type="default" @click="goBack">
          ← 返回列表
        </el-button>
      </div>

      <div v-loading="loading">
        <div v-if="detail" class="detail-grid-layout">
          <!-- Left side panels -->
          <div class="left-panels">
            <!-- 1. Basic Info Panel -->
            <div class="admin-card">
              <div class="profile-header">
                <el-avatar
                  v-if="detail.avatarUrl"
                  :size="64"
                  :src="detail.avatarUrl"
                  class="profile-avatar"
                />
                <div v-else class="profile-avatar-placeholder">
                  {{ getInitials(detail.name) }}
                </div>
                <div class="profile-title-row">
                  <h2 class="profile-name">{{ detail.name }}</h2>
                  <el-tag size="small" type="primary">
                    {{ getRoleCn(detail.primaryRole) }}
                  </el-tag>
                </div>
              </div>

              <div class="description-list mt-20">
                <div class="description-item">
                  <span class="desc-label">OpenID</span>
                  <span class="desc-value clickable-openid" title="点击复制 OpenID" @click="copyOpenId(detail.openId)">
                    {{ truncateOpenId(detail.openId) }}
                    <el-icon class="copy-icon"><CopyDocument /></el-icon>
                  </span>
                </div>
                <div class="description-item">
                  <span class="desc-label">手机号</span>
                  <span class="desc-value">{{ maskPhone(detail.phone) }}</span>
                </div>
                <div class="description-item">
                  <span class="desc-label">注册时间</span>
                  <span class="desc-value">{{ formatTime(detail.createdAt) }}</span>
                </div>
                <div class="description-item">
                  <span class="desc-label">最近登录</span>
                  <span class="desc-value">{{ formatTime(detail.lastLoginAt) }}</span>
                </div>
              </div>
            </div>

            <!-- 2. School Profile Panel -->
            <div class="admin-card">
              <div class="card-header-bar-custom">
                <h3 class="card-title">学校档案</h3>
              </div>
              <div class="card-body-custom">
                <div v-if="detail.schoolProfile" class="description-list">
                  <div class="description-item">
                    <span class="desc-label">学院</span>
                    <span class="desc-value">{{ detail.schoolProfile.college }}</span>
                  </div>
                  <div class="description-item">
                    <span class="desc-label">
                      {{ detail.primaryRole === 'student' ? '学号' : '工号' }}
                    </span>
                    <span class="desc-value">
                      {{ detail.schoolProfile.studentId || detail.schoolProfile.staffId || '—' }}
                    </span>
                  </div>
                  <div class="description-item">
                    <span class="desc-label">绑定状态</span>
                    <span class="desc-value">
                      <el-tag :type="getBindingTagType(detail.schoolProfile.bindingStatus)" size="small">
                        {{ getBindingStatusCn(detail.schoolProfile.bindingStatus) }}
                      </el-tag>
                    </span>
                  </div>
                </div>
                <div v-else class="empty-section">
                  未绑定学校档案
                </div>
              </div>
            </div>

            <!-- 3. Emergency Rescuer certification Panel -->
            <div class="admin-card">
              <div class="card-header-bar-custom">
                <h3 class="card-title">急救资格</h3>
              </div>
              <div class="card-body-custom">
                <div v-if="detail.rescuerSummary" class="description-list">
                  <div class="description-item">
                    <span class="desc-label">类型</span>
                    <span class="desc-value">
                      {{ detail.rescuerSummary.responderType === 'rescuer' ? '急救员' : '值班校医' }}
                    </span>
                  </div>
                  <div class="description-item">
                    <span class="desc-label">等级</span>
                    <span class="desc-value">{{ getLevelCn(detail.rescuerSummary.level) }}</span>
                  </div>
                  <div class="description-item">
                    <span class="desc-label">证书号</span>
                    <span class="desc-value">{{ maskCertNo(detail.rescuerSummary.currentCertificateNo) }}</span>
                  </div>
                  <div class="description-item">
                    <span class="desc-label">有效期至</span>
                    <span class="desc-value">{{ formatDateOnly(detail.rescuerSummary.certExpiry) }}</span>
                  </div>
                </div>
                <div v-else class="empty-section">
                  未绑定急救资格
                </div>
              </div>
            </div>
          </div>

          <!-- Right side Editor Panel -->
          <div class="right-panel">
            <div class="admin-card editor-card">
              <h3 class="editor-title">编辑信息</h3>
              <el-form :model="editForm" label-position="top" class="edit-form">
                <!-- Primary Role select -->
                <el-form-item label="身份">
                  <el-select v-model="editForm.primaryRole" class="full-width">
                    <el-option label="学生" value="student" />
                    <el-option label="教师" value="teacher" />
                    <el-option label="职工" value="staff" />
                    <el-option label="管理员" value="admin" />
                  </el-select>
                </el-form-item>

                <!-- Account Status Select -->
                <el-form-item label="账号状态">
                  <el-select v-model="editForm.accountStatus" class="full-width">
                    <el-option label="正常" value="active" />
                    <el-option label="禁用" value="disabled" />
                  </el-select>
                </el-form-item>

                <!-- Responder qualifications status select -->
                <el-form-item label="资格状态">
                  <el-select v-model="editForm.responderStatus" class="full-width">
                    <el-option label="已激活" value="active" />
                    <el-option label="未激活" value="inactive" />
                  </el-select>
                </el-form-item>

                <!-- Certified Select -->
                <el-form-item label="已认证">
                  <el-select v-model="editForm.certified" class="full-width">
                    <el-option label="是" :value="true" />
                    <el-option label="否" :value="false" />
                  </el-select>
                </el-form-item>

                <!-- Rescuer Level Select -->
                <el-form-item label="等级">
                  <el-select v-model="editForm.level" class="full-width" placeholder="请选择等级" clearable>
                    <el-option label="无" :value="null" />
                    <el-option label="荣誉" value="honor" />
                    <el-option label="见习" value="trainee" />
                    <el-option label="正式" value="official" />
                    <el-option label="core 核心" value="core" />
                  </el-select>
                </el-form-item>

                <!-- Admin Remark Input -->
                <el-form-item label="备注">
                  <el-input
                    v-model="editForm.adminRemark"
                    type="textarea"
                    :rows="3"
                    placeholder="请输入管理备注信息"
                  />
                </el-form-item>

                <!-- Save Action Button -->
                <el-form-item class="save-form-item">
                  <el-button
                    type="primary"
                    :loading="saveLoading"
                    class="full-width"
                    @click="handleSave"
                  >
                    保存修改
                  </el-button>
                </el-form-item>
              </el-form>
            </div>
          </div>
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { CopyDocument } from '@element-plus/icons-vue'
import adminLayout from '@/components/admin-layout.vue'
import { getUserDetail, updateUser } from '@/api/users'
import type { AdminUserDetail, AdminUserUpdatePayload, PrimaryRole, AccountStatus, ResponderStatus, RescuerLevel } from '@/types/user'
import { getApiErrorMessage } from '@/api/client'

const route = useRoute()
const router = useRouter()
const openIdParam = route.params.openId as string

const loading = ref(true)
const saveLoading = ref(false)
const detail = ref<AdminUserDetail | null>(null)

// Editing states matching Payload properties
const editForm = reactive({
  primaryRole: 'student' as PrimaryRole,
  accountStatus: 'active' as AccountStatus,
  responderStatus: 'inactive' as ResponderStatus,
  certified: false,
  level: null as RescuerLevel | null,
  adminRemark: '',
})

const roleMap: Record<string, string> = {
  student: '学生',
  teacher: '教师',
  staff: '职工',
  admin: '管理员',
}

const levelMap: Record<string, string> = {
  honor: '荣誉',
  trainee: '见习',
  official: '正式',
  core: '核心',
}

const bindingMap: Record<string, string> = {
  pending: '待审核',
  active: '已绑定',
  rejected: '已拒绝',
  replaced: '已替换',
  unbound: '已解绑',
}

function getRoleCn(role: string) {
  return roleMap[role] || role
}

function getLevelCn(level: string | null) {
  if (!level) return '—'
  return levelMap[level] || level
}

function getBindingStatusCn(status: string) {
  return bindingMap[status] || status
}

function getBindingTagType(status: string) {
  if (status === 'active') return 'success'
  if (status === 'pending') return 'warning'
  if (status === 'rejected' || status === 'unbound') return 'danger'
  return 'info'
}

function goBack() {
  router.push('/users')
}

// Convert UTC dates to formatting
function formatTime(dateStr: string | null) {
  if (!dateStr) return '—'
  const date = new Date(dateStr)
  if (isNaN(date.getTime())) return dateStr
  const yyyy = date.getFullYear()
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const dd = String(date.getDate()).padStart(2, '0')
  const hh = String(date.getHours()).padStart(2, '0')
  const min = String(date.getMinutes()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd} ${hh}:${min}`
}

function formatDateOnly(dateStr: string | null | undefined) {
  if (!dateStr) return '—'
  const date = new Date(dateStr)
  if (isNaN(date.getTime())) return dateStr
  const yyyy = date.getFullYear()
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const dd = String(date.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

function getInitials(name: string) {
  if (!name) return 'U'
  return name.charAt(0)
}

// Truncates long OpenID strings for clean listing view
function truncateOpenId(id: string) {
  if (!id) return ''
  if (id.length <= 12) return id
  return `${id.slice(0, 6)}...${id.slice(-6)}`
}

// Masks mobile phone digits
function maskPhone(phone: string | null) {
  if (!phone) return '—'
  return phone.replace(/(\d{3})\d{4}(\d{4})/, '$1****$2')
}

// Masks certification records digits
function maskCertNo(certNo: string | null | undefined) {
  if (!certNo) return '—'
  if (certNo.length <= 8) return certNo
  return `${certNo.slice(0, 8)}****`
}

// Copy OpenID value
function copyOpenId(text: string) {
  if (!text) return
  navigator.clipboard.writeText(text).then(() => {
    ElMessage.success('OpenID 已成功复制到剪贴板')
  }).catch((err) => {
    console.error(err)
    ElMessage.error('复制失败，请重试')
  })
}

async function loadDetail() {
  loading.value = true
  try {
    const res = await getUserDetail(openIdParam)
    detail.value = res.data.data

    // Hydrate form states
    editForm.primaryRole = res.data.data.primaryRole
    editForm.accountStatus = res.data.data.accountStatus
    editForm.responderStatus = res.data.data.responderStatus || 'inactive'
    editForm.certified = res.data.data.certified
    editForm.level = res.data.data.level
    editForm.adminRemark = res.data.data.adminRemark || ''
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '获取用户详情信息失败'))
  } finally {
    loading.value = false
  }
}

// Handles edits update submitting only modified parameters (delta check)
async function handleSave() {
  if (!detail.value) return

  saveLoading.value = true
  try {
    const payload: AdminUserUpdatePayload = {}
    let hasChanges = false

    if (editForm.primaryRole !== detail.value.primaryRole) {
      payload.primaryRole = editForm.primaryRole
      hasChanges = true
    }
    if (editForm.accountStatus !== detail.value.accountStatus) {
      payload.accountStatus = editForm.accountStatus
      hasChanges = true
    }
    const currentResponderStatus = detail.value.responderStatus || 'inactive'
    if (editForm.responderStatus !== currentResponderStatus) {
      payload.responderStatus = editForm.responderStatus
      hasChanges = true
    }
    if (editForm.certified !== detail.value.certified) {
      payload.certified = editForm.certified
      hasChanges = true
    }
    if (editForm.level !== detail.value.level) {
      payload.level = editForm.level
      hasChanges = true
    }
    const currentRemark = detail.value.adminRemark || ''
    if (editForm.adminRemark !== currentRemark) {
      payload.adminRemark = editForm.adminRemark
      hasChanges = true
    }

    if (!hasChanges) {
      ElMessage.warning('内容未做任何修改')
      saveLoading.value = false
      return
    }

    await updateUser(openIdParam, payload)
    ElMessage.success('保存修改成功')
    await loadDetail() // Refresh info
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '更新用户信息失败'))
  } finally {
    saveLoading.value = false
  }
}

onMounted(() => {
  loadDetail()
})
</script>

<style scoped lang="scss">
.detail-container {
  padding: 24px;

  @media (max-width: 767px) {
    padding: 12px;
  }
}

.detail-header-action {
  margin-bottom: 20px;
}

// Two columns detail grid
.detail-grid-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 360px;
  gap: 20px;

  @media (max-width: 1023px) {
    grid-template-columns: 1fr;
  }
}

.left-panels {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.right-panel {
  flex-shrink: 0;
}

// Profiles elements
.profile-header {
  display: flex;
  align-items: center;
  gap: 16px;
  border-bottom: 1px dashed #ebeef5;
  padding-bottom: 16px;
}

.profile-avatar {
  flex-shrink: 0;
}

.profile-avatar-placeholder {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background-color: #c0c4cc;
  color: #ffffff;
  font-size: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-weight: 500;
}

.profile-title-row {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.profile-name {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.clickable-openid {
  cursor: pointer;
  color: #409eff;
  display: inline-flex;
  align-items: center;
  gap: 4px;

  &:hover {
    color: #73b8ff;
    text-decoration: underline;
  }

  .copy-icon {
    font-size: 12px;
  }
}

.card-header-bar-custom {
  padding: 16px 24px;
  border-bottom: 1px solid #ebeef5;
}

.card-title {
  margin: 0;
  font-size: 15px;
  font-weight: 500;
  color: #303133;
}

.card-body-custom {
  padding: 20px 24px;
}

.editor-card {
  padding: 20px 24px;
}

.editor-title {
  margin: 0 0 20px 0;
  font-size: 15px;
  font-weight: 500;
  color: #303133;
  padding-bottom: 16px;
  border-bottom: 1px solid #ebeef5;
}

.edit-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.full-width {
  width: 100%;
}

.save-form-item {
  margin-top: 12px;
  margin-bottom: 0;
}

.empty-section {
  font-size: 14px;
  color: #909399;
  text-align: center;
  padding: 12px 0;
}

.mt-20 {
  margin-top: 20px;
}

.full-width {
  width: 100%;
}
</style>
