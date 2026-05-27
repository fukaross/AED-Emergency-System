<template>
  <admin-layout>
    <div class="sessions-container">
      <div v-loading="loading" class="admin-card sessions-card">
        <h3 class="sessions-title">当前登录设备</h3>

        <div class="sessions-list">
          <div
            v-for="session in sessions"
            :key="session.id"
            class="session-item"
          >
            <!-- Left Info -->
            <div class="session-info">
              <div class="session-ua">
                {{ session.userAgentSummary || '未知设备 / 浏览器' }}
              </div>
              <div class="session-meta">
                <span>IP: {{ session.maskedIpAddress }}</span>
                <span class="meta-divider">|</span>
                <span>登录于: {{ formatTime(session.createdAt) }}</span>
                <span class="meta-divider">|</span>
                <span>过期于: {{ formatTime(session.expiresAt) }}</span>
              </div>
            </div>

            <!-- Right Action -->
            <div class="session-actions">
              <el-tag
                v-if="session.current"
                type="primary"
                size="small"
                class="current-tag"
              >
                当前
              </el-tag>
              <el-button
                v-else
                link
                type="danger"
                @click="handleRevoke(session)"
              >
                登出
              </el-button>
            </div>
          </div>
        </div>

        <!-- Only current device placeholder -->
        <div v-if="sessions.length <= 1" class="only-current-tip">
          当前仅有此设备登录
        </div>
      </div>

      <!-- Logout current device -->
      <div class="logout-action-row">
        <el-button
          type="danger"
          plain
          :loading="logoutLoading"
          @click="handleLogout"
        >
          登出当前设备
        </el-button>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessageBox, ElMessage } from 'element-plus'
import AdminLayout from '@/components/admin-layout.vue'
import { getSessions, deleteSession, logout } from '@/api/auth'
import { useAdminStore } from '@/stores/admin'
import type { AdminSessionListItem } from '@/types/auth'
import { getApiErrorMessage } from '@/api/client'

const router = useRouter()
const adminStore = useAdminStore()

const loading = ref(false)
const logoutLoading = ref(false)
const sessions = ref<AdminSessionListItem[]>([])

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

async function fetchSessions() {
  loading.value = true
  try {
    const res = await getSessions()
    sessions.value = res.data.data
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '获取会话列表失败'))
  } finally {
    loading.value = false
  }
}

function handleRevoke(session: AdminSessionListItem) {
  ElMessageBox.confirm(
    '确定要登出该设备？',
    '提示',
    {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning',
    }
  ).then(async () => {
    try {
      await deleteSession(session.id)
      ElMessage.success('设备已强退')
      fetchSessions()
    } catch (error: unknown) {
      console.error(error)
      ElMessage.error(getApiErrorMessage(error, '操作失败'))
    }
  }).catch(() => {})
}

function handleLogout() {
  ElMessageBox.confirm(
    '确定要登出当前设备并返回登录页面吗？',
    '提示',
    {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning',
    }
  ).then(async () => {
    logoutLoading.value = true
    try {
      await logout()
      adminStore.clearSession()
      ElMessage.success('已安全退出登录')
      router.push('/login')
    } catch (error: unknown) {
      console.error(error)
      ElMessage.error(getApiErrorMessage(error, '登出失败'))
    } finally {
      logoutLoading.value = false
    }
  }).catch(() => {})
}

onMounted(() => {
  fetchSessions()
})
</script>

<style scoped lang="scss">
.sessions-container {
  padding: 24px;

  @media (max-width: 767px) {
    padding: 12px;
  }
}

.sessions-card {
  background-color: #ffffff;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.06);
  padding: 24px;
}

.sessions-title {
  margin: 0 0 16px 0;
  font-size: 15px;
  font-weight: 500;
  color: #303133;
  padding-bottom: 16px;
  border-bottom: 1px solid #ebeef5;
}

.sessions-list {
  display: flex;
  flex-direction: column;
}

.session-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 0;

  & + .session-item {
    border-top: 1px solid #ebeef5;
  }
}

.session-info {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.session-ua {
  font-size: 14px;
  color: #303133;
  font-weight: 500;
}

.session-meta {
  font-size: 13px;
  color: #909399;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;
}

.meta-divider {
  margin: 0 4px;
  color: #dcdfe6;
}

.session-actions {
  flex-shrink: 0;
  margin-left: 16px;
}

.current-tag {
  background-color: #ecf5ff;
  border-color: #ecf5ff;
  color: #409eff;
  font-weight: 500;
}

.only-current-tip {
  font-size: 13px;
  color: #909399;
  padding-top: 8px;
}

.logout-action-row {
  margin-top: 20px;
}
</style>
