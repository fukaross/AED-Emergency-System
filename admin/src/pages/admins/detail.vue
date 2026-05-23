<template>
  <admin-layout>
    <div class="admin-detail-container">
      <div class="page-actions">
        <el-button :icon="ArrowLeft" plain @click="router.push('/admins')">
          返回
        </el-button>
      </div>

      <div v-loading="loading" class="admin-card detail-card">
        <div class="detail-header">
          <div>
            <h3 class="detail-title">{{ detail?.username || '管理员详情' }}</h3>
            <p class="detail-id">{{ detail?.id || '—' }}</p>
          </div>
          <el-button
            v-if="detail && canReset"
            :icon="RefreshLeft"
            :loading="resetLoading"
            type="warning"
            plain
            @click="handleResetPassword"
          >
            重置密码
          </el-button>
        </div>

        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="用户名">{{ detail.username }}</el-descriptions-item>
          <el-descriptions-item label="账号 ID">{{ detail.id }}</el-descriptions-item>
          <el-descriptions-item label="角色">
            <status-tag category="admin" :status="detail.role" />
          </el-descriptions-item>
          <el-descriptions-item label="状态">
            <status-tag category="account" :status="detail.status" />
          </el-descriptions-item>
          <el-descriptions-item label="创建时间">{{ formatTime(detail.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="更新时间">{{ formatTime(detail.updatedAt) }}</el-descriptions-item>
          <el-descriptions-item label="会话废弃时间">{{ formatTime(detail.sessionsRevokedAt) }}</el-descriptions-item>
          <el-descriptions-item label="登录锁定至">{{ formatTime(detail.lockedUntil) }}</el-descriptions-item>
        </el-descriptions>
      </div>

      <div v-if="detail" class="admin-card sessions-card">
        <h3 class="section-title">活跃会话</h3>
        <div v-if="detail.activeSessions.length" class="sessions-list">
          <div
            v-for="session in detail.activeSessions"
            :key="session.id"
            class="session-item"
          >
            <div class="session-info">
              <div class="session-ua">
                {{ session.userAgentSummary || '未知设备 / 浏览器' }}
              </div>
              <div class="session-meta">
                <span>IP: {{ session.maskedIpAddress }}</span>
                <span>登录于: {{ formatTime(session.createdAt) }}</span>
                <span>过期于: {{ formatTime(session.expiresAt) }}</span>
                <span>最近活跃: {{ formatTime(session.lastSeenAt) }}</span>
              </div>
            </div>
            <el-tag v-if="session.current" type="primary" size="small">
              当前
            </el-tag>
          </div>
        </div>
        <el-empty v-else description="暂无活跃会话" />
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft, RefreshLeft } from '@element-plus/icons-vue'
import AdminLayout from '@/components/admin-layout.vue'
import StatusTag from '@/components/status-tag.vue'
import { getAdminDetail, resetAdminPassword, type AdminAccountDetail } from '@/api/admins'
import { getApiErrorMessage } from '@/api/client'
import { useAdminStore } from '@/stores/admin'

const route = useRoute()
const router = useRouter()
const adminStore = useAdminStore()

const loading = ref(false)
const resetLoading = ref(false)
const detail = ref<AdminAccountDetail | null>(null)

const adminId = computed(() => String(route.params.id || ''))
const canReset = computed(() => {
  return detail.value?.role === 'admin' && detail.value.username !== adminStore.username
})

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

async function fetchDetail() {
  loading.value = true
  try {
    const res = await getAdminDetail(adminId.value)
    detail.value = res.data.data
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '获取管理员详情失败'))
  } finally {
    loading.value = false
  }
}

function handleResetPassword() {
  if (!detail.value) return
  ElMessageBox.prompt(
    `请输入管理员 ${detail.value.username} 的新密码。提交后该账号所有活跃会话会立即失效。`,
    '确认重置密码',
    {
      confirmButtonText: '确认重置',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--warning',
      inputType: 'password',
      inputValidator: validatePassword,
    }
  ).then(({ value }) => resetPassword(value)).catch(() => {})
}

function validatePassword(value: string) {
  return value && value.length >= 8 ? true : '密码长度最短为 8 位'
}

async function resetPassword(password: string) {
  if (!detail.value) return
  resetLoading.value = true
  try {
    await resetAdminPassword(detail.value.id, { password })
    ElMessage.success('密码已重置')
    fetchDetail()
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '重置密码失败'))
  } finally {
    resetLoading.value = false
  }
}

onMounted(() => {
  fetchDetail()
})
</script>

<style scoped lang="scss">
.admin-detail-container {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.page-actions {
  display: flex;
  justify-content: flex-start;
}

.detail-card,
.sessions-card { padding: 24px; }

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 20px;
  gap: 16px;
}

.detail-title {
  margin: 0 0 6px 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.detail-id {
  margin: 0;
  color: #909399;
  font-size: 13px;
  word-break: break-all;
}

.section-title {
  margin: 0 0 16px 0;
  font-size: 15px;
  font-weight: 500;
  color: #303133;
}

.sessions-list {
  display: flex;
  flex-direction: column;
}

.session-item {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  padding: 14px 0;

  & + .session-item {
    border-top: 1px solid #ebeef5;
  }
}

.session-info {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.session-ua {
  font-size: 14px;
  color: #303133;
  font-weight: 500;
}

.session-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  color: #909399;
  font-size: 13px;
}

</style>
