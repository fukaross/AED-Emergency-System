<template>
  <admin-layout>
    <div class="page-content">
      <div class="admin-card no-padding">
        <!-- Card Header Title -->
        <div class="card-header-bar">
          <h3 class="card-header-title">用户列表</h3>
        </div>

        <!-- Filter Bar -->
        <div class="filter-bar filter-section">
          <!-- Role select -->
          <el-select
            v-model="filterRole"
            placeholder="身份"
            clearable
            style="width: 120px;"
            @change="handleFilterChange"
          >
            <el-option label="全部身份" value="" />
            <el-option label="学生" value="student" />
            <el-option label="教师" value="teacher" />
            <el-option label="职工" value="staff" />
            <el-option label="管理员" value="admin" />
          </el-select>

          <!-- Account status select -->
          <el-select
            v-model="filterStatus"
            placeholder="账号状态"
            clearable
            style="width: 120px;"
            @change="handleFilterChange"
          >
            <el-option label="全部状态" value="" />
            <el-option label="正常" value="active" />
            <el-option label="禁用" value="disabled" />
          </el-select>

          <!-- Keyword search -->
          <el-input
            v-model="searchKeyword"
            placeholder="搜索姓名、手机号..."
            clearable
            style="width: 220px;"
            @clear="handleFilterChange"
            @keyup.enter="handleFilterChange"
          />

          <!-- Query Button -->
          <el-button type="primary" size="small" @click="handleFilterChange">
            查询
          </el-button>
        </div>

        <!-- User Data Table -->
        <el-table
          v-loading="loading"
          :data="tableData"
          style="width: 100%;"
          class="data-table"
        >
          <!-- User Profile Column (Avatar + Name) -->
          <el-table-column
            label="用户"
            width="180"
            align="left"
          >
            <template #default="{ row }">
              <div class="user-profile-cell">
                <el-avatar
                  v-if="row.avatarUrl"
                  :size="32"
                  :src="row.avatarUrl"
                  class="user-avatar"
                />
                <div v-else class="avatar-placeholder">
                  {{ getInitials(row.name) }}
                </div>
                <span class="user-name">{{ row.name }}</span>
              </div>
            </template>
          </el-table-column>

          <!-- Role Column -->
          <el-table-column
            prop="primaryRole"
            label="身份"
            width="80"
            align="center"
          >
            <template #default="{ row }">
              {{ getRoleCn(row.primaryRole) }}
            </template>
          </el-table-column>

          <!-- Certification Column -->
          <el-table-column
            prop="certified"
            label="资格"
            width="80"
            align="center"
          >
            <template #default="{ row }">
              <el-tag
                :type="row.certified ? 'success' : 'info'"
                size="small"
                class="tag-small"
              >
                {{ row.certified ? '已认证' : '未认证' }}
              </el-tag>
            </template>
          </el-table-column>

          <!-- Level Column -->
          <el-table-column
            prop="level"
            label="等级"
            width="80"
            align="center"
          >
            <template #default="{ row }">
              {{ getLevelCn(row.level) }}
            </template>
          </el-table-column>

          <!-- Account Status Column -->
          <el-table-column
            prop="accountStatus"
            label="账号状态"
            width="90"
            align="center"
          >
            <template #default="{ row }">
              <status-tag :status="row.accountStatus" category="account" />
            </template>
          </el-table-column>

          <!-- Update time Column -->
          <el-table-column
            prop="updatedAt"
            label="更新时间"
            width="120"
            align="left"
          >
            <template #default="{ row }">
              {{ formatTime(row.updatedAt) }}
            </template>
          </el-table-column>

          <!-- Actions Column -->
          <el-table-column
            label="操作"
            width="80"
            align="center"
            fixed="right"
          >
            <template #default="{ row }">
              <el-button text type="primary" size="small" @click="viewDetail(row.openId)">
                详情
              </el-button>
            </template>
          </el-table-column>

          <template #empty>
            <el-empty description="暂无符合条件的用户记录" />
          </template>
        </el-table>

        <!-- Pagination -->
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
import { getUsers } from '@/api/users'
import type { AdminUserListItem } from '@/types/user'
import { getApiErrorMessage } from '@/api/client'

const route = useRoute()
const router = useRouter()

const loading = ref(false)
const tableData = ref<AdminUserListItem[]>([])
const totalItems = ref(0)
const pageSize = ref(10)

// Filters states
const filterRole = ref('')
const filterStatus = ref('')
const searchKeyword = ref('')
const currentPage = ref(1)

const roleMap: Record<string, string> = {
  student: '学生',
  teacher: '教师',
  staff: '职工',
  admin: '管理员',
}

const levelMap: Record<string, string> = {
  honor: '荣誉',
  trainee: '见习',
  official: '正式',
  core: '核心',
}

function getRoleCn(role: string) {
  return roleMap[role] || role
}

function getLevelCn(level: string | null) {
  if (!level) return '—'
  return levelMap[level] || level
}

// Extract initials for fallback avatar (gets the first char)
function getInitials(name: string) {
  if (!name) return 'U'
  return name.charAt(0)
}

function viewDetail(openId: string) {
  router.push(`/users/${openId}`)
}

// Convert UTC dates to local MM-DD HH:mm formatting
function formatTime(dateStr: string | null) {
  if (!dateStr) return '—'
  const date = new Date(dateStr)
  if (isNaN(date.getTime())) return dateStr
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const dd = String(date.getDate()).padStart(2, '0')
  const hh = String(date.getHours()).padStart(2, '0')
  const min = String(date.getMinutes()).padStart(2, '0')
  return `${mm}-${dd} ${hh}:${min}`
}

// Sync local inputs from URL parameters
function syncFromRoute() {
  filterRole.value = (route.query.role as string) || ''
  filterStatus.value = (route.query.status as string) || ''
  searchKeyword.value = (route.query.keyword as string) || ''
  currentPage.value = Number(route.query.page) || 1
}

// Push local inputs to URL parameters (drives Watch handler load)
function updateRouteQuery(resetPage = false) {
  const query: LocationQueryRaw = {}
  if (filterRole.value) query.role = filterRole.value
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

async function loadData() {
  loading.value = true
  try {
    const res = await getUsers({
      page: currentPage.value,
      pageSize: pageSize.value,
      keyword: searchKeyword.value || undefined,
      accountStatus: filterStatus.value || undefined,
      primaryRole: filterRole.value || undefined,
    })
    tableData.value = res.data.data.items
    totalItems.value = res.data.data.total
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '获取用户列表数据失败'))
  } finally {
    loading.value = false
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

.user-profile-cell {
  display: flex;
  align-items: center;
}

.user-avatar {
  margin-right: 10px;
  flex-shrink: 0;
}

.avatar-placeholder {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background-color: #c0c4cc;
  color: #ffffff;
  font-size: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 10px;
  flex-shrink: 0;
  font-weight: 500;
}

.user-name {
  font-size: 14px;
  color: #303133;
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tag-small {
  border-radius: 3px;
  font-size: 12px;
  padding: 2px 8px;
}
</style>
