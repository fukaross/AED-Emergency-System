<template>
  <admin-layout>
    <div class="audit-logs-container">
      <div class="admin-card no-padding">
        <!-- Card Header Bar -->
        <div class="card-header-bar">
          <span class="card-header-title">操作日志</span>
        </div>

        <!-- Filter Bar -->
        <div class="filter-bar filter-section">
          <!-- Event Type Filter -->
          <el-select
            v-model="filterForm.eventType"
            placeholder="事件类型"
            clearable
            class="filter-select"
            @change="handleSearch"
          >
            <el-option label="所有事件类型" value="" />
            <el-option label="管理 (admin)" value="admin" />
            <el-option label="求助 (help)" value="help" />
            <el-option label="用户 (user)" value="user" />
          </el-select>

          <!-- Actor Filter -->
          <el-select
            v-model="filterForm.actor"
            placeholder="操作人"
            clearable
            class="filter-select"
            @change="handleSearch"
          >
            <el-option label="所有操作人" value="" />
            <el-option
              v-for="username in actorOptions"
              :key="username"
              :label="username"
              :value="username"
            />
          </el-select>

          <!-- Keyword Search -->
          <el-input
            v-model="filterForm.keyword"
            placeholder="搜索事件名称、摘要..."
            clearable
            class="filter-input"
            @keyup.enter="handleSearch"
          />

          <!-- Query Button -->
          <el-button type="primary" size="small" @click="handleSearch">
            查询
          </el-button>
        </div>

        <!-- Logs Table -->
        <el-table
          v-loading="loading"
          :data="logsList"
          style="width: 100%"
        >
          <!-- Expand Column -->
          <el-table-column type="expand">
            <template #default="{ row }">
              <div class="expand-detail-container">
                <div class="metadata-block">
                  <!-- User IP address masked -->
                  <div class="meta-item">
                    <span class="meta-label">IP 地址：</span>
                    <span class="meta-value">{{ maskIp(row.ipAddress) }}</span>
                  </div>

                  <!-- Target ID if exists -->
                  <div v-if="row.targetId" class="meta-item">
                    <span class="meta-label">目标 ID：</span>
                    <span class="meta-value"><code>{{ row.targetId }}</code></span>
                  </div>

                  <!-- Metadata Changed fields tags -->
                  <div v-if="row.metadata && row.metadata.changedFields && Array.isArray(row.metadata.changedFields)" class="meta-item">
                    <span class="meta-label">修改字段：</span>
                    <span class="meta-value">
                      <el-tag
                        v-for="field in row.metadata.changedFields"
                        :key="field"
                        size="small"
                        class="meta-field-tag"
                      >
                        {{ field }}
                      </el-tag>
                    </span>
                  </div>

                  <!-- Full Pretty Metadata JSON representation -->
                  <div class="meta-item">
                    <span class="meta-label">附加元数据：</span>
                    <div class="meta-value">
                      <pre class="metadata-json">{{ formatJson(row.metadata) }}</pre>
                    </div>
                  </div>
                </div>
              </div>
            </template>
          </el-table-column>

          <!-- Column DateTime -->
          <el-table-column label="时间" width="150">
            <template #default="{ row }">
              {{ formatTimeSeconds(row.createdAt) }}
            </template>
          </el-table-column>

          <!-- Column Actor -->
          <el-table-column label="操作人" width="100">
            <template #default="{ row }">
              <span class="actor-name">{{ row.actorName || '—' }}</span>
            </template>
          </el-table-column>

          <!-- Column Event Type -->
          <el-table-column label="事件类型" width="100">
            <template #default="{ row }">
              <span
                class="event-type-tag"
                :style="getEventTypeStyle(row.eventType)"
              >
                {{ getEventTypeLabel(row.eventType) }}
              </span>
            </template>
          </el-table-column>

          <!-- Column Event Name -->
          <el-table-column label="事件名称" width="160">
            <template #default="{ row }">
              {{ getEventNameCn(row.eventName) }}
            </template>
          </el-table-column>

          <!-- Column Summary -->
          <el-table-column label="摘要">
            <template #default="{ row }">
              <span class="summary-text" :title="row.summary || ''">{{ row.summary || '—' }}</span>
            </template>
          </el-table-column>
        </el-table>

        <!-- Pagination -->
        <div class="pagination-container">
          <el-pagination
            v-model:current-page="currentPage"
            v-model:page-size="pageSize"
            :page-sizes="[20, 50, 100]"
            :total="total"
            small
            layout="total, prev, pager, next, sizes"
            @current-change="handlePageChange"
            @size-change="handleSizeChange"
          />
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, reactive, watch, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import AdminLayout from '@/components/admin-layout.vue'
import { getAuditLogs } from '@/api/audit-logs'
import { getAdmins } from '@/api/admins'
import type { AdminAuditLogRecord } from '@/types/audit-log'
import { getApiErrorMessage } from '@/api/client'

const route = useRoute()
const router = useRouter()

const loading = ref(false)
const logsList = ref<AdminAuditLogRecord[]>([])
const total = ref(0)
const currentPage = ref(1)
const pageSize = ref(20)

// Actor select options
const actorOptions = ref<string[]>()

const filterForm = reactive({
  eventType: '',
  actor: '',
  keyword: '',
})

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

function getEventTypeStyle(type: string) {
  if (type === 'admin') return { background: '#ecf5ff', color: '#409eff', border: '1px solid #ecf5ff' }
  if (type === 'help') return { background: '#fdf6ec', color: '#e6a23c', border: '1px solid #fdf6ec' }
  if (type === 'user') return { background: '#f0f9eb', color: '#67c23a', border: '1px solid #f0f9eb' }
  return { background: '#f4f4f5', color: '#909399', border: '1px solid #f4f4f5' }
}

function getEventTypeLabel(type: string) {
  if (type === 'admin') return '管理'
  if (type === 'help') return '求助'
  if (type === 'user') return '用户'
  return type
}

function formatTimeSeconds(dateStr: string | null) {
  if (!dateStr) return '—'
  const date = new Date(dateStr)
  if (isNaN(date.getTime())) return dateStr
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const dd = String(date.getDate()).padStart(2, '0')
  const hh = String(date.getHours()).padStart(2, '0')
  const min = String(date.getMinutes()).padStart(2, '0')
  const ss = String(date.getSeconds()).padStart(2, '0')
  return `${mm}-${dd} ${hh}:${min}:${ss}`
}

function maskIp(ip: string | null) {
  if (!ip) return '—'
  const v4Match = ip.match(/^(\d+)\.(\d+)\.(\d+)\.(\d+)$/)
  if (v4Match) {
    return `${v4Match[1]}.${v4Match[2]}.*.*`
  }
  const parts = ip.split(':')
  if (parts.length > 2) {
    return `${parts[0]}:${parts[1]}:*:*`
  }
  return ip
}

function formatJson(metadata: Record<string, unknown> | null) {
  if (!metadata) return '无附加信息'
  const copy: Record<string, unknown> = { ...metadata }
  delete copy.changedFields
  if (Object.keys(copy).length === 0) return '无额外属性'
  return JSON.stringify(copy, null, 2)
}

async function fetchAdminsList() {
  try {
    const res = await getAdmins()
    actorOptions.value = res.data.data.map(item => item.username)
  } catch (error) {
    console.error('获取管理员列表失败', error)
  }
}

async function fetchLogs() {
  loading.value = true
  try {
    const res = await getAuditLogs({
      page: currentPage.value,
      pageSize: pageSize.value,
      eventType: filterForm.eventType || undefined,
      actor: filterForm.actor || undefined,
      keyword: filterForm.keyword || undefined,
    })
    logsList.value = res.data.data.items
    total.value = res.data.data.total
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '获取操作日志失败'))
  } finally {
    loading.value = false
  }
}

// Synced query filter values and page state
watch(
  () => route.query,
  (query) => {
    filterForm.eventType = (query.eventType as string) || ''
    filterForm.actor = (query.actor as string) || ''
    filterForm.keyword = (query.keyword as string) || ''
    currentPage.value = parseInt(query.page as string) || 1
    pageSize.value = parseInt(query.pageSize as string) || 20
    fetchLogs()
  },
  { immediate: true }
)

function updateRouteQuery(page: number) {
  router.push({
    path: '/audit-logs',
    query: {
      page,
      pageSize: pageSize.value,
      eventType: filterForm.eventType || undefined,
      actor: filterForm.actor || undefined,
      keyword: filterForm.keyword || undefined,
    },
  })
}

function handleSearch() {
  updateRouteQuery(1)
}

function handlePageChange(val: number) {
  updateRouteQuery(val)
}

function handleSizeChange(val: number) {
  pageSize.value = val
  updateRouteQuery(1)
}

onMounted(() => {
  fetchAdminsList()
})
</script>

<style scoped lang="scss">
.audit-logs-container {
  padding: 24px;
}

.filter-section {
  padding-top: 16px;
}

.filter-select {
  width: 130px;
}

.filter-input {
  width: 220px;
}

.actor-name {
  font-size: 14px;
  color: #303133;
  font-weight: 500;
}

.event-type-tag {
  display: inline-block;
  padding: 2px 8px;
  font-size: 12px;
  border-radius: 3px;
  font-weight: 500;
}

.summary-text {
  font-size: 14px;
  color: #606266;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  display: inline-block;
  max-width: 100%;
}

// Expand details area formatting
.expand-detail-container {
  background-color: #fafafa;
  padding: 16px 24px;
  margin: -16px -24px; // cancel el-table default padding if possible or just use normal spacing
  border-top: 1px solid #f0f2f5;
  border-bottom: 1px solid #f0f2f5;
}

.metadata-block {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.meta-item {
  display: flex;
  align-items: flex-start;
  font-size: 13px;
  line-height: 20px;

  .meta-label {
    width: 90px;
    font-weight: 500;
    color: #909399;
    flex-shrink: 0;
  }

  .meta-value {
    color: #303133;
    word-break: break-all;
    flex-grow: 1;

    code {
      background-color: #f0f2f5;
      padding: 2px 6px;
      border-radius: 4px;
      font-family: monospace;
      font-size: 12px;
    }
  }
}

.meta-field-tag {
  margin-right: 6px;
  margin-bottom: 4px;
}

.metadata-json {
  margin: 4px 0 0 0;
  padding: 12px;
  background-color: #f4f4f5;
  border-radius: 6px;
  font-family: monospace;
  font-size: 12px;
  color: #303133;
  overflow-x: auto;
  white-space: pre-wrap;
  max-height: 200px;
}
</style>
