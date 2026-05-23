<template>
  <admin-layout>
    <div class="dashboard-container">
      <!-- Skeletons for Stat Cards -->
      <el-skeleton :loading="loading" animated>
        <template #template>
          <div class="stat-grid">
            <div v-for="i in 4" :key="i" class="skeleton-card">
              <el-skeleton-item variant="text" style="width: 40%" />
              <el-skeleton-item variant="h1" style="width: 60%; margin-top: 12px; height: 32px" />
              <el-skeleton-item variant="text" style="width: 50%; margin-top: 8px" />
            </div>
          </div>
        </template>

        <template #default>
          <div class="stat-grid">
            <!-- Total Users Card -->
            <div class="stat-card">
              <span class="stat-label">总用户数</span>
              <span class="stat-val">{{ data.usersTotal }}</span>
              <span class="stat-sub">系统注册用户总量</span>
            </div>

            <!-- Total Help Requests Card -->
            <div class="stat-card">
              <span class="stat-label">总求助数</span>
              <span class="stat-val">{{ data.helpRequestsTotal }}</span>
              <span class="stat-sub">累计紧急求助事件</span>
            </div>

            <!-- Pending Review Card (Highlight in Red, Clickable) -->
            <div class="stat-card is-danger-card" @click="goToPendingReview">
              <span class="stat-label">待审核求助</span>
              <span class="stat-val highlight-danger">{{ data.pendingReviewTotal }}</span>
              <span class="stat-sub">点击立即查看处理</span>
            </div>

            <!-- Today's New Requests Card -->
            <div class="stat-card">
              <span class="stat-label">今日新增求助</span>
              <span class="stat-val">{{ data.todayNewHelpRequests }}</span>
              <span class="stat-sub">今天发生的求助数量</span>
            </div>
          </div>
        </template>
      </el-skeleton>

      <!-- Recent Log Card Section -->
      <div class="admin-card logs-card">
        <div class="card-header-bar-custom">
          <h3 class="card-title">最近操作日志</h3>
        </div>

        <el-skeleton :loading="loading" :rows="8" animated>
          <template #default>
            <div v-if="data.recentLogs && data.recentLogs.length > 0" class="log-list">
              <div v-for="log in data.recentLogs" :key="log.id" class="log-item">
                <div class="log-time">{{ formatTime(log.createdAt) }}</div>
                <div class="log-actor">{{ log.actorName || '系统' }}</div>
                <div class="log-event">
                  <span :class="['event-tag', getEventTagClass(log.eventType)]">
                    {{ getEventNameCn(log.eventName) }}
                  </span>
                </div>
                <div class="log-summary">{{ log.summary }}</div>
              </div>
            </div>
            <el-empty v-else description="暂无最近操作日志" />
          </template>
        </el-skeleton>

        <!-- Redirect link to all logs - only visible to super admin -->
        <div v-if="isSuperAdmin && !loading" class="logs-footer">
          <router-link to="/audit-logs" class="all-logs-link">
            查看全部日志 →
          </router-link>
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import { getDashboard } from '@/api/dashboard'
import { useAdminStore } from '@/stores/admin'
import type { AdminDashboardResponse } from '@/types/dashboard'
import { getApiErrorMessage } from '@/api/client'

const router = useRouter()
const store = useAdminStore()
const loading = ref(true)

const data = ref<AdminDashboardResponse>({
  usersTotal: 0,
  helpRequestsTotal: 0,
  pendingReviewTotal: 0,
  todayNewHelpRequests: 0,
  recentLogs: [],
})

const isSuperAdmin = computed(() => store.isSuperAdmin)

// Event mapping dictionary
const eventNameMap: Record<string, string> = {
  'admin.bootstrap_created': '系统初始化',
  'admin.login': '管理员登录',
  'admin.logout': '管理员登出',
  'admin.session_revoked': '会话撤销',
  'admin.super_admin_reset': '超管重置',
  'admin.password_reset': '管理员密码重置',
  'admin.admin_created': '创建管理员',
  'admin.admin_deleted': '删除管理员',
  'admin.created': '创建管理员',
  'admin.deleted': '删除管理员',
  'help.create': '发布求助',
  'help.report_submitted': '提交报告',
  'help.close': '关闭求助',
  'help.false_alarm': '标记误报',
  'user.updated': '用户更新',
}

function getEventNameCn(name: string) {
  return eventNameMap[name] || name
}

function getEventTagClass(type: string) {
  if (type === 'admin') return 'tag-admin'
  if (type === 'help') return 'tag-help'
  if (type === 'user') return 'tag-user'
  return ''
}

// Convert UTC dates to local MM-DD HH:mm formats
function formatTime(dateStr: string) {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  if (isNaN(date.getTime())) return dateStr
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const dd = String(date.getDate()).padStart(2, '0')
  const hh = String(date.getHours()).padStart(2, '0')
  const min = String(date.getMinutes()).padStart(2, '0')
  return `${mm}-${dd} ${hh}:${min}`
}

function goToPendingReview() {
  router.push({
    path: '/help-requests',
    query: { status: 'pending_review' },
  })
}

async function loadDashboardData() {
  loading.value = true
  try {
    const res = await getDashboard()
    data.value = res.data.data
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '获取仪表盘数据失败，请重试'))
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadDashboardData()
})
</script>

<style scoped lang="scss">
.dashboard-container {
  padding: 24px;
}

// Stat Card Grid system
.stat-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 20px;
  margin-bottom: 20px;

  @media (max-width: 1200px) {
    grid-template-columns: repeat(2, 1fr);
  }
}

.stat-card, .skeleton-card {
  background-color: #ffffff;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.06);
  padding: 20px 24px;
  display: flex;
  flex-direction: column;
}

.stat-card {
  &.is-danger-card {
    cursor: pointer;
    transition: transform 0.2s, box-shadow 0.2s;

    &:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.12);
    }
  }
}

.skeleton-card {
  height: 120px;
}

.stat-label {
  font-size: 13px;
  color: #909399;
}

.stat-val {
  font-size: 28px;
  font-weight: 600;
  color: #303133;
  margin-top: 8px;
  line-height: 1;
}

.highlight-danger {
  color: #f56c6c;
}

.stat-sub {
  font-size: 12px;
  color: #909399;
  margin-top: 6px;
}

// Logs styling
.logs-card {
  margin-top: 20px;
  padding: 0; // overrides default admin-card padding
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

.log-list {
  display: flex;
  flex-direction: column;
}

.log-item {
  display: flex;
  align-items: center;
  padding: 12px 24px;
  border-bottom: 1px solid #ebeef5;
  font-size: 14px;

  &:last-child {
    border-bottom: none;
  }

  &:hover {
    background-color: #f5f7fa;
  }
}

.log-time {
  width: 110px;
  font-size: 13px;
  color: #909399;
  flex-shrink: 0;
}

.log-actor {
  width: 120px;
  color: #303133;
  font-weight: 500;
  flex-shrink: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.log-event {
  width: 140px;
  flex-shrink: 0;
}

.event-tag {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 3px;
  font-size: 12px;
  line-height: 1.5;

  &.tag-admin {
    background-color: #ecf5ff;
    color: #409eff;
  }

  &.tag-help {
    background-color: #fdf6ec;
    color: #e6a23c;
  }

  &.tag-user {
    background-color: #f0f9eb;
    color: #67c23a;
  }
}

.log-summary {
  flex: 1;
  color: #606266;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.logs-footer {
  display: flex;
  justify-content: flex-end;
  padding: 16px 24px;
  border-top: 1px solid #ebeef5;
}

.all-logs-link {
  font-size: 13px;
  color: #409eff;
  text-decoration: none;
  cursor: pointer;

  &:hover {
    color: #73b8ff;
    text-decoration: underline;
  }
}
</style>
