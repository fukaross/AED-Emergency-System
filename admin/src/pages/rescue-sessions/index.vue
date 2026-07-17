<template>
  <admin-layout>
    <div class="page-content">
      <div class="admin-card no-padding">
        <div class="card-header-bar">
          <h3 class="card-header-title">急救会话监控</h3>
        </div>

        <el-tabs v-model="activeTab" class="session-tabs" @tab-change="handleTabChange">
          <el-tab-pane label="进行中" name="active" />
          <el-tab-pane label="历史存档" name="history" />
        </el-tabs>

        <div class="table-responsive">
          <el-table v-loading="loading" :data="tableData" style="width: 100%;" class="data-table">
            <el-table-column prop="requestNo" label="求助编号" width="160" align="left" />
            <el-table-column prop="address" label="位置" min-width="160" align="left" show-overflow-tooltip />
            <el-table-column prop="helpStatus" label="求助状态" width="100" align="center">
              <template #default="{ row }">
                <status-tag :status="row.helpStatus" category="help" />
              </template>
            </el-table-column>
            <el-table-column prop="participantsCount" label="参与者" width="90" align="center">
              <template #default="{ row }">{{ row.participantsCount }}人</template>
            </el-table-column>
            <el-table-column prop="messagesCount" label="消息数" width="90" align="center" />
            <el-table-column prop="lastActivityAt" label="最近活动" width="150" align="left">
              <template #default="{ row }">{{ formatTime(row.lastActivityAt) || '—' }}</template>
            </el-table-column>
            <el-table-column prop="createdAt" label="发布时间" width="150" align="left">
              <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
            </el-table-column>
            <el-table-column label="操作" width="90" align="center" fixed="right">
              <template #default="{ row }">
                <el-button text type="primary" size="small" @click="viewDetail(row.helpId)">
                  监控
                </el-button>
              </template>
            </el-table-column>

            <template #empty>
              <el-empty :description="activeTab === 'active' ? '暂无进行中的急救会话' : '暂无历史会话存档'" />
            </template>
          </el-table>
        </div>

        <div class="pagination-container">
          <el-pagination
            v-model:current-page="currentPage"
            :page-size="pageSize"
            :total="totalItems"
            small
            layout="total, prev, pager, next"
            @current-change="handlePageChange"
          />
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getRescueSessions } from '@/api/rescue-sessions'
import { getApiErrorMessage } from '@/api/client'
import { createRequestSequencer } from '@/utils/request-sequencer'
import type { AdminRescueSessionListItem } from '@/types/rescue-session'

const POLL_INTERVAL_MS = 10_000

type SessionTab = 'active' | 'history'

const router = useRouter()

const loading = ref(false)
const tableData = ref<AdminRescueSessionListItem[]>([])
const totalItems = ref(0)
const pageSize = ref(10)
const currentPage = ref(1)
const activeTab = ref<SessionTab>('active')

let pollTimer: ReturnType<typeof setInterval> | null = null
let isPolling = false
const requestSequencer = createRequestSequencer()

function handleTabChange() {
  currentPage.value = 1
  loadData()
}

function handlePageChange(page: number) {
  currentPage.value = page
  loadData()
}

function viewDetail(helpId: string) {
  router.push(`/rescue-sessions/${helpId}`)
}

function formatTime(dateStr: string | null): string {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  if (isNaN(date.getTime())) return dateStr
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const dd = String(date.getDate()).padStart(2, '0')
  const hh = String(date.getHours()).padStart(2, '0')
  const min = String(date.getMinutes()).padStart(2, '0')
  return `${mm}-${dd} ${hh}:${min}`
}

async function fetchData(showError: boolean) {
  const ticket = requestSequencer.next()
  try {
    const res = await getRescueSessions({
      status: activeTab.value,
      page: currentPage.value,
      pageSize: pageSize.value,
    })
    if (!requestSequencer.isCurrent(ticket)) return
    tableData.value = res.data.data.items
    totalItems.value = res.data.data.total
  } catch (error: unknown) {
    if (!requestSequencer.isCurrent(ticket)) return
    if (showError) {
      console.error(error)
      ElMessage.error(getApiErrorMessage(error, '加载急救会话列表失败'))
    }
  }
}

async function loadData() {
  loading.value = true
  try {
    await fetchData(true)
  } finally {
    loading.value = false
  }
}

async function pollData() {
  if (isPolling) return
  isPolling = true
  try {
    await fetchData(false)
  } finally {
    isPolling = false
  }
}

function startPolling() {
  stopPolling()
  pollTimer = setInterval(pollData, POLL_INTERVAL_MS)
}

function stopPolling() {
  if (pollTimer) { clearInterval(pollTimer); pollTimer = null }
  isPolling = false
}

function handleVisibilityChange() {
  if (document.hidden) {
    stopPolling()
  } else {
    pollData()
    startPolling()
  }
}

onMounted(() => {
  loadData()
  startPolling()
  document.addEventListener('visibilitychange', handleVisibilityChange)
})

onUnmounted(() => {
  stopPolling()
  document.removeEventListener('visibilitychange', handleVisibilityChange)
})
</script>

<style scoped lang="scss">
.page-content {
  padding: 24px;

  @media (max-width: 767px) {
    padding: 12px;
  }
}

.session-tabs {
  padding: 0 16px;
  background-color: #ffffff;

  :deep(.el-tabs__header) {
    margin-bottom: 0;
  }
}

.data-table {
  border-top: 1px solid #ebeef5;
}
</style>
