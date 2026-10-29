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
        <div class="table-responsive">
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
        </div>

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
import { logApiError } from '@/utils/safe-api-error'
import {
  formatJson,
  formatTimeSeconds,
  getEventNameCn,
  getEventTypeLabel,
  getEventTypeStyle,
  maskIp,
} from './audit-log-presentation'

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

async function fetchAdminsList() {
  try {
    const res = await getAdmins()
    actorOptions.value = res.data.data.map(item => item.username)
  } catch (error) {
    logApiError({ context: 'audit-logs.admin-options', error, fallback: '获取管理员列表失败' })
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
    logApiError({ context: 'audit-logs.list', error, fallback: '获取操作日志失败' })
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

<style scoped lang="scss" src="./index.scss"></style>
