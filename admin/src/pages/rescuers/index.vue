<template>
  <admin-layout>
    <div class="page-content-padding">
      <div class="admin-card no-padding">
        <div class="card-header-bar">
          <h3 class="card-header-title">急救员管理</h3>
        </div>

        <div class="filter-bar">
          <el-select
            v-model="filterCampus"
            placeholder="校区"
            clearable
            style="width: 130px"
            @change="handleFilterChange"
          >
            <el-option label="全部" value="" />
            <el-option label="REDACTED" value="guilinyang" />
            <el-option label="府城" value="fucheng" />
          </el-select>
          <el-select
            v-model="filterLevel"
            placeholder="等级"
            clearable
            style="width: 130px"
            @change="handleFilterChange"
          >
            <el-option label="全部" value="" />
            <el-option label="荣誉急救员" value="honor" />
            <el-option label="预备急救员" value="trainee" />
            <el-option label="正式急救员" value="official" />
            <el-option label="骨干急救员" value="core" />
          </el-select>
          <el-select
            v-model="filterStatus"
            placeholder="资格状态"
            clearable
            style="width: 130px"
            @change="handleFilterChange"
          >
            <el-option label="全部" value="" />
            <el-option label="已激活" value="active" />
            <el-option label="未激活" value="inactive" />
          </el-select>
          <el-input
            v-model="searchKeyword"
            placeholder="搜索姓名/编号/证书号"
            clearable
            style="width: 220px"
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
            <el-table-column prop="id" label="编号" width="110" />
            <el-table-column label="头像" width="70">
              <template #default="{ row }">
                <el-avatar :src="row.avatarUrl" :size="36" />
              </template>
            </el-table-column>
            <el-table-column prop="name" label="姓名" width="90" />
            <el-table-column label="校区" width="80">
              <template #default="{ row }">
                <span>{{ row.campus === 'fucheng' ? '府城' : 'REDACTED' }}</span>
              </template>
            </el-table-column>
            <el-table-column label="等级" width="110">
              <template #default="{ row }">
                <span>{{ levelLabel(row.level) }}</span>
              </template>
            </el-table-column>
            <el-table-column label="资格状态" width="90">
              <template #default="{ row }">
                <status-tag :status="row.qualificationStatus" category="responder" />
              </template>
            </el-table-column>
            <el-table-column prop="certificateNoMasked" label="证书号" width="190" show-overflow-tooltip />
            <el-table-column label="技能" min-width="150">
              <template #default="{ row }">
                <span class="skill-tag" :class="{ active: row.skillCpr }">CPR</span>
                <span class="skill-tag" :class="{ active: row.skillAed }">AED</span>
                <span class="skill-tag" :class="{ active: row.skillStb }">止血包扎</span>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="80" fixed="right">
              <template #default="{ row }">
                <el-button text type="primary" size="small" @click="goDetail(row.id)">详情</el-button>
              </template>
            </el-table-column>
            <template #empty>
              <el-empty description="暂无急救员记录" />
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
import { getRescuers } from '@/api/rescuers'
import { getApiErrorMessage } from '@/api/client'
import { createRequestSequencer } from '@/utils/request-sequencer'
import type { AdminRescuerItem } from '@/types/rescuer'

const LEVEL_LABELS: Record<string, string> = {
  honor: '荣誉急救员',
  trainee: '预备急救员',
  official: '正式急救员',
  core: '骨干急救员',
}

function levelLabel(level: string | null): string {
  if (!level) return '-'
  return LEVEL_LABELS[level] || level
}

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const tableData = ref<AdminRescuerItem[]>([])
const total = ref(0)
const currentPage = ref(1)
const pageSize = 20

const filterCampus = ref<string>('')
const filterLevel = ref<string>('')
const filterStatus = ref<string>('')
const searchKeyword = ref<string>('')

const pollTimer = ref<ReturnType<typeof setInterval> | null>(null)
const sequencer = createRequestSequencer()

function syncFromRoute() {
  const query = route.query
  filterCampus.value = (query.campus as string) || ''
  filterLevel.value = (query.level as string) || ''
  filterStatus.value = (query.qualificationStatus as string) || ''
  searchKeyword.value = (query.keyword as string) || ''
  currentPage.value = Number(query.page) || 1
}

function firstQueryValue(value: LocationQuery[string] | LocationQueryRaw[string]): string {
  if (Array.isArray(value)) return value[0]?.toString() ?? ''
  return value?.toString() ?? ''
}

function routeQueryKey(query: LocationQuery | LocationQueryRaw): string {
  return [
    firstQueryValue(query.campus),
    firstQueryValue(query.level),
    firstQueryValue(query.qualificationStatus),
    firstQueryValue(query.keyword),
    firstQueryValue(query.page),
  ].join('\n')
}

function buildRouteQuery(): LocationQueryRaw {
  const query: Record<string, string> = {}
  if (filterCampus.value) query.campus = filterCampus.value
  if (filterLevel.value) query.level = filterLevel.value
  if (filterStatus.value) query.qualificationStatus = filterStatus.value
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
    const res = await getRescuers({
      page: currentPage.value,
      pageSize,
      campus: filterCampus.value || undefined,
      level: filterLevel.value || undefined,
      qualificationStatus: filterStatus.value || undefined,
      keyword: searchKeyword.value || undefined,
    })
    if (!sequencer.isCurrent(seq)) return
    const body = res.data.data
    tableData.value = body.items
    total.value = body.total
  } catch (error) {
    if (!sequencer.isCurrent(seq)) return
    ElMessage.error(getApiErrorMessage(error, '加载急救员列表失败'))
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
  router.push(`/rescuers/${id}`)
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
.skill-tag {
  display: inline-block;
  padding: 2px 8px;
  margin-right: 6px;
  border-radius: 4px;
  font-size: 12px;
  background: #f4f4f5;
  color: #909399;
}

.skill-tag.active {
  background: #ecf5ff;
  color: #409eff;
}
</style>
