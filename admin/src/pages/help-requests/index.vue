<template>
  <admin-layout>
    <div class="page-content">
      <div class="admin-card no-padding">
        <div class="card-header-bar">
          <h3 class="card-header-title">求助列表</h3>
        </div>

        <div class="filter-bar filter-section">
          <el-select
            v-model="filterStatus"
            placeholder="全部状态"
            clearable
            style="width: 140px;"
            @change="handleFilterChange"
          >
            <el-option label="全部状态" value="" />
            <el-option label="已发布" value="published" />
            <el-option label="响应中" value="responding" />
            <el-option label="待审核" value="pending_review" />
            <el-option label="已关闭" value="closed" />
            <el-option label="误报" value="false_alarm" />
          </el-select>

          <el-input
            v-model="searchKeyword"
            placeholder="搜索求助内容、编号..."
            clearable
            style="width: 240px;"
            @clear="handleFilterChange"
            @keyup.enter="handleFilterChange"
          />

          <el-button type="primary" size="small" @click="handleFilterChange">
            查询
          </el-button>
        </div>

        <div class="table-responsive">
          <el-table
            v-loading="loading"
            :data="tableData"
            style="width: 100%;"
            class="data-table"
          >
            <el-table-column
              prop="requestNo"
              label="编号"
              width="160"
              align="left"
            />

            <el-table-column
              prop="content"
              label="内容"
              min-width="200"
              align="left"
              show-overflow-tooltip
            />

            <el-table-column
              prop="requestChannel"
              label="渠道"
              width="80"
              align="center"
            >
              <template #default="{ row }">
                <status-tag :status="row.requestChannel" category="channel" />
              </template>
            </el-table-column>

            <el-table-column
              prop="status"
              label="状态"
              width="100"
              align="center"
            >
              <template #default="{ row }">
                <status-tag :status="row.status" category="help" />
              </template>
            </el-table-column>

            <el-table-column
              prop="address"
              label="位置"
              width="140"
              align="left"
              show-overflow-tooltip
            />

            <el-table-column
              prop="reportersCount"
              label="报告上传者"
              width="110"
              align="center"
            >
              <template #default="{ row }">
                {{ row.reportersCount || 0 }}人
              </template>
            </el-table-column>

            <el-table-column
              prop="createdAt"
              label="时间"
              width="150"
              align="left"
            >
              <template #default="{ row }">
                {{ formatTime(row.createdAt) }}
              </template>
            </el-table-column>

            <el-table-column
              label="操作"
              width="80"
              align="center"
              fixed="right"
            >
              <template #default="{ row }">
                <el-button text type="primary" size="small" @click="viewDetail(row.id)">
                  详情
                </el-button>
              </template>
            </el-table-column>

            <template #empty>
              <el-empty description="暂无符合要求的求助记录" />
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
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { LocationQueryRaw } from 'vue-router'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getHelpRequests } from '@/api/help'
import type { AdminHelpListItem } from '@/types/help'
import { getApiErrorMessage } from '@/api/client'
import { createRequestSequencer } from '@/utils/request-sequencer'
import { logApiError } from '@/utils/safe-api-error'
import { formatCompactDateTime as formatTime } from '@/utils/date-time'

const POLL_INTERVAL_MS = 10_000

const route = useRoute()
const router = useRouter()

const loading = ref(false)
const tableData = ref<AdminHelpListItem[]>([])
const totalItems = ref(0)
const pageSize = ref(10)

const filterStatus = ref('')
const searchKeyword = ref('')
const currentPage = ref(1)

let pollTimer: ReturnType<typeof setInterval> | null = null
let isPolling = false
const requestSequencer = createRequestSequencer()

function syncFromRoute() {
  filterStatus.value = (route.query.status as string) || ''
  searchKeyword.value = (route.query.keyword as string) || ''
  currentPage.value = Number(route.query.page) || 1
}

function updateRouteQuery(resetPage = false) {
  const query: LocationQueryRaw = {}
  if (filterStatus.value) query.status = filterStatus.value
  if (searchKeyword.value) query.keyword = searchKeyword.value
  query.page = resetPage ? 1 : currentPage.value

  router.push({
    path: route.path,
    query,
  })
}

function handleFilterChange() {
  updateRouteQuery(true)
}

function handlePageChange(page: number) {
  currentPage.value = page
  updateRouteQuery(false)
}

function viewDetail(id: string) {
  router.push(`/help-requests/${id}`)
}

async function loadData() {
  loading.value = true
  const ticket = requestSequencer.next()
  try {
    const res = await getHelpRequests({
      page: currentPage.value,
      pageSize: pageSize.value,
      status: filterStatus.value || undefined,
      keyword: searchKeyword.value || undefined,
    })
    if (!requestSequencer.isCurrent(ticket)) return
    tableData.value = res.data.data.items
    totalItems.value = res.data.data.total
  } catch (error: unknown) {
    if (!requestSequencer.isCurrent(ticket)) return
    logApiError({ context: 'help-requests.list', error, fallback: '加载求助列表数据失败' })
    ElMessage.error(getApiErrorMessage(error, '加载求助列表数据失败'))
  } finally {
    loading.value = false
  }
}

async function pollData() {
  if (isPolling) return
  isPolling = true
  const ticket = requestSequencer.next()
  try {
    const res = await getHelpRequests({
      page: currentPage.value,
      pageSize: pageSize.value,
      status: filterStatus.value || undefined,
      keyword: searchKeyword.value || undefined,
    })
    if (!requestSequencer.isCurrent(ticket)) return
    tableData.value = res.data.data.items
    totalItems.value = res.data.data.total
  } catch (error: unknown) {
    logApiError({ context: 'help-requests.list.poll', error, fallback: '求助列表刷新失败' })
  }
  finally { isPolling = false }
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

watch(
  () => route.query,
  () => {
    syncFromRoute()
    loadData()
  }
)

onMounted(() => {
  syncFromRoute()
  loadData()
  startPolling()
  document.addEventListener('visibilitychange', handleVisibilityChange)
})

onUnmounted(() => {
  stopPolling()
  document.removeEventListener('visibilitychange', handleVisibilityChange)
})
</script>

<style scoped lang="scss" src="./index.scss"></style>
