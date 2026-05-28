<template>
  <admin-layout>
    <div class="page-content-padding">
      <div class="admin-card no-padding">
        <div class="card-header-bar">
          <h3 class="card-header-title">AED 设备管理</h3>
          <el-button type="primary" size="small" @click="goCreate">
            + 新增 AED
          </el-button>
        </div>

        <div class="filter-bar">
          <el-select
            v-model="filterStatus"
            placeholder="设备状态"
            clearable
            style="width: 140px"
            @change="handleFilterChange"
          >
            <el-option label="全部" value="" />
            <el-option label="在线" value="online" />
            <el-option label="离线" value="offline" />
          </el-select>
          <el-input
            v-model="searchKeyword"
            placeholder="搜索编号/名称/地址"
            clearable
            style="width: 240px"
            @keyup.enter="handleFilterChange"
            @clear="handleFilterChange"
          >
            <template #prefix>
              <el-icon><Search /></el-icon>
            </template>
          </el-input>
          <el-button type="primary" @click="handleFilterChange">查询</el-button>
        </div>

        <div class="table-responsive">
          <el-table v-loading="loading" :data="tableData" class="data-table" stripe>
            <el-table-column prop="id" label="编号" width="100" />
            <el-table-column prop="name" label="名称" min-width="120" />
            <el-table-column label="坐标" width="200">
              <template #default="{ row }">
                <span class="coord-text">{{ formatCoord(row.lat) }}, {{ formatCoord(row.lng) }}</span>
              </template>
            </el-table-column>
            <el-table-column label="状态" width="80">
              <template #default="{ row }">
                <status-tag :status="row.status" category="device" />
              </template>
            </el-table-column>
            <el-table-column prop="statusNote" label="备注" min-width="100" show-overflow-tooltip>
              <template #default="{ row }">
                <span v-if="row.statusNote" class="status-note-text">{{ row.statusNote }}</span>
                <span v-else class="text-muted">-</span>
              </template>
            </el-table-column>
            <el-table-column prop="address" label="地址" min-width="160" show-overflow-tooltip />
            <el-table-column prop="lastMaintenance" label="上次维护" width="110" />
            <el-table-column label="操作" width="140" fixed="right">
              <template #default="{ row }">
                <el-button text type="primary" size="small" @click="goDetail(row.id)">详情</el-button>
                <el-popconfirm
                  title="确定删除该设备？"
                  confirm-button-text="删除"
                  cancel-button-text="取消"
                  @confirm="handleDelete(row)"
                >
                  <template #reference>
                    <el-button text type="danger" size="small">删除</el-button>
                  </template>
                </el-popconfirm>
              </template>
            </el-table-column>
            <template #empty>
              <el-empty description="暂无 AED 设备记录" />
            </template>
          </el-table>
        </div>

        <div class="pagination-container">
          <el-pagination
            v-model:current-page="currentPage"
            :page-size="pageSize"
            :total="total"
            small
            background
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
import type { LocationQuery, LocationQueryRaw } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Search } from '@element-plus/icons-vue'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getAedDevices, deleteAedDevice } from '@/api/devices'
import { getApiErrorMessage } from '@/api/client'
import { createRequestSequencer } from '@/utils/request-sequencer'
import type { AdminAedDeviceItem } from '@/types/device'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const tableData = ref<AdminAedDeviceItem[]>([])
const total = ref(0)
const currentPage = ref(1)
const pageSize = 20

const filterStatus = ref<string>('')
const searchKeyword = ref<string>('')

const pollTimer = ref<ReturnType<typeof setInterval> | null>(null)
const sequencer = createRequestSequencer()

function formatCoord(val: number): string {
  return val.toFixed(5)
}

function syncFromRoute() {
  const query = route.query
  filterStatus.value = (query.status as string) || ''
  searchKeyword.value = (query.keyword as string) || ''
  currentPage.value = Number(query.page) || 1
}

function firstQueryValue(value: LocationQuery[string] | LocationQueryRaw[string]): string {
  if (Array.isArray(value)) return value[0]?.toString() ?? ''
  return value?.toString() ?? ''
}

function routeQueryKey(query: LocationQuery | LocationQueryRaw): string {
  return [
    firstQueryValue(query.status),
    firstQueryValue(query.keyword),
    firstQueryValue(query.page),
  ].join('\n')
}

function buildRouteQuery(): LocationQueryRaw {
  const query: Record<string, string> = {}
  if (filterStatus.value) query.status = filterStatus.value
  if (searchKeyword.value) query.keyword = searchKeyword.value
  if (currentPage.value > 1) query.page = String(currentPage.value)
  return query
}

async function syncRouteQueryOrLoad() {
  const query = buildRouteQuery()
  if (routeQueryKey(route.query) === routeQueryKey(query)) {
    loadData()
    return
  }
  await router.replace({ query })
}

async function loadData() {
  loading.value = true
  const seq = sequencer.next()
  try {
    const res = await getAedDevices({
      page: currentPage.value,
      pageSize,
      status: filterStatus.value || undefined,
      keyword: searchKeyword.value || undefined,
    })
    if (!sequencer.isCurrent(seq)) return
    const body = res.data.data
    tableData.value = body.items
    total.value = body.total
  } catch (error) {
    if (!sequencer.isCurrent(seq)) return
    ElMessage.error(getApiErrorMessage(error, '加载 AED 设备列表失败'))
  } finally {
    if (sequencer.isCurrent(seq)) {
      loading.value = false
    }
  }
}

async function handleFilterChange() {
  currentPage.value = 1
  await syncRouteQueryOrLoad()
}

async function handlePageChange(page: number) {
  currentPage.value = page
  await syncRouteQueryOrLoad()
}

function goDetail(id: string) {
  router.push(`/devices/${id}`)
}

function goCreate() {
  router.push('/devices/new')
}

async function handleDelete(row: AdminAedDeviceItem) {
  try {
    await deleteAedDevice(row.id)
    ElMessage.success(`设备 ${row.name} 已删除`)
    loadData()
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '删除失败'))
  }
}

function startPolling() {
  stopPolling()
  pollTimer.value = setInterval(loadData, 10000)
}

function stopPolling() {
  if (pollTimer.value) {
    clearInterval(pollTimer.value)
    pollTimer.value = null
  }
}

function onVisibilityChange() {
  if (document.hidden) {
    stopPolling()
  } else {
    loadData()
    startPolling()
  }
}

onMounted(() => {
  syncFromRoute()
  loadData()
  startPolling()
  document.addEventListener('visibilitychange', onVisibilityChange)
})

watch(
  () => route.query,
  () => {
    syncFromRoute()
    loadData()
  },
)

onUnmounted(() => {
  stopPolling()
  document.removeEventListener('visibilitychange', onVisibilityChange)
})
</script>

<style scoped>
.coord-text {
  font-size: 13px;
  color: #606266;
  font-family: 'Courier New', monospace;
}

.status-note-text {
  color: #e6a23c;
  font-size: 13px;
}

.text-muted {
  color: #c0c4cc;
}
</style>
