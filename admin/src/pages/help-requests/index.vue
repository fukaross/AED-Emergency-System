<template>
  <admin-layout>
    <div class="page-content">
      <div class="admin-card no-padding">
        <!-- Title bar -->
        <div class="card-header-bar">
          <h3 class="card-header-title">求助列表</h3>
        </div>

        <!-- Filters section -->
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

        <!-- Data table -->
        <el-table
          v-loading="loading"
          :data="tableData"
          style="width: 100%;"
          class="data-table"
        >
          <!-- Request No. Column -->
          <el-table-column
            prop="requestNo"
            label="编号"
            width="160"
            align="left"
          />

          <!-- Content Column -->
          <el-table-column
            prop="content"
            label="内容"
            min-width="200"
            align="left"
            show-overflow-tooltip
          />

          <!-- Channel Column -->
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

          <!-- Status Column -->
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

          <!-- Address Column -->
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

          <!-- Timestamp Column -->
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

          <!-- Operation Column -->
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

        <!-- Table pagination -->
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
import { ref, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { LocationQueryRaw } from 'vue-router'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getHelpRequests } from '@/api/help'
import type { AdminHelpListItem } from '@/types/help'
import { getApiErrorMessage } from '@/api/client'

const route = useRoute()
const router = useRouter()

const loading = ref(false)
const tableData = ref<AdminHelpListItem[]>([])
const totalItems = ref(0)
const pageSize = ref(10)

// Filters & paging state
const filterStatus = ref('')
const searchKeyword = ref('')
const currentPage = ref(1)

// Synchronize component state with route query params
function syncFromRoute() {
  filterStatus.value = (route.query.status as string) || ''
  searchKeyword.value = (route.query.keyword as string) || ''
  currentPage.value = Number(route.query.page) || 1
}

// Push local filters to router query (drives the search flow)
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

// Triggered when search criteria changes
function handleFilterChange() {
  updateRouteQuery(true)
}

// Triggered when changing page index
function handlePageChange(page: number) {
  currentPage.value = page
  updateRouteQuery(false)
}

function viewDetail(id: string) {
  router.push(`/help-requests/${id}`)
}

// Format UTC dates to MM-DD HH:mm format
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

async function loadData() {
  loading.value = true
  try {
    const res = await getHelpRequests({
      page: currentPage.value,
      pageSize: pageSize.value,
      status: filterStatus.value || undefined,
      keyword: searchKeyword.value || undefined,
    })
    tableData.value = res.data.data.items
    totalItems.value = res.data.data.total
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '加载求助列表数据失败'))
  } finally {
    loading.value = false
  }
}

// Listen to route query changes to load corresponding data
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
})
</script>

<style scoped lang="scss">
.page-content {
  padding: 24px;
}

.filter-section {
  padding-top: 16px;
  background-color: #ffffff;
}

.data-table {
  border-top: 1px solid #ebeef5;
}
</style>
