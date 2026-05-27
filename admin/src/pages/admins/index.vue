<template>
  <admin-layout>
    <div class="admins-container">
      <div class="admin-card no-padding">
        <div class="card-header-bar">
          <span class="card-header-title">管理员列表</span>
          <el-button :icon="Plus" type="primary" size="small" @click="goToCreate">
            创建管理员
          </el-button>
        </div>

        <div class="table-responsive">
          <el-table v-loading="loading" :data="adminsList" style="width: 100%">
            <el-table-column label="用户名" width="160">
              <template #default="{ row }">
                <span class="username-text">{{ row.username }}</span>
              </template>
            </el-table-column>

            <el-table-column label="角色" width="120">
              <template #default="{ row }">
                <status-tag category="admin" :status="row.role" />
              </template>
            </el-table-column>

            <el-table-column label="状态" width="100">
              <template #default="{ row }">
                <status-tag category="account" :status="row.status" />
              </template>
            </el-table-column>

            <el-table-column label="创建时间" width="160">
              <template #default="{ row }">
                {{ formatTime(row.createdAt) }}
              </template>
            </el-table-column>

            <el-table-column label="操作" width="240" fixed="right">
              <template #default="{ row }">
                <div class="action-buttons">
                  <el-button
                    :icon="View"
                    link
                    type="primary"
                    @click="goToDetail(row)"
                  >
                    详情
                  </el-button>
                  <template v-if="canManage(row)">
                    <el-button
                      :icon="RefreshLeft"
                      :loading="resetLoadingId === row.id"
                      link
                      type="warning"
                      @click="handleResetPassword(row)"
                    >
                      重置
                    </el-button>
                    <el-button
                      :icon="Delete"
                      link
                      type="danger"
                      @click="handleDelete(row)"
                    >
                      删除
                    </el-button>
                  </template>
                  <span v-else class="disabled-action">—</span>
                </div>
              </template>
            </el-table-column>
          </el-table>
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessageBox, ElMessage } from 'element-plus'
import { Delete, Plus, RefreshLeft, View } from '@element-plus/icons-vue'
import AdminLayout from '@/components/admin-layout.vue'
import StatusTag from '@/components/status-tag.vue'
import {
  getAdmins,
  deleteAdmin,
  resetAdminPassword,
  type AdminAccountListItem,
} from '@/api/admins'
import { useAdminStore } from '@/stores/admin'
import { getApiErrorMessage } from '@/api/client'

const router = useRouter()
const adminStore = useAdminStore()

const loading = ref(false)
const resetLoadingId = ref<string | null>(null)
const adminsList = ref<AdminAccountListItem[]>([])

function canManage(row: AdminAccountListItem) {
  return row.username !== adminStore.username && row.role === 'admin'
}

function goToCreate() {
  router.push('/admins/create')
}

function goToDetail(row: AdminAccountListItem) {
  router.push(`/admins/${row.id}`)
}

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

async function fetchAdmins() {
  loading.value = true
  try {
    const res = await getAdmins()
    adminsList.value = res.data.data
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '获取管理员列表失败'))
  } finally {
    loading.value = false
  }
}

function handleResetPassword(row: AdminAccountListItem) {
  ElMessageBox.prompt(
    `请输入管理员 ${row.username} 的新密码。提交后该账号所有活跃会话会立即失效。`,
    '确认重置密码',
    {
      confirmButtonText: '确认重置',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--warning',
      inputType: 'password',
      inputValidator: validatePassword,
    }
  ).then(async ({ value }) => {
    resetLoadingId.value = row.id
    try {
      await resetAdminPassword(row.id, { password: value })
      ElMessage.success('密码已重置')
    } catch (error: unknown) {
      console.error(error)
      ElMessage.error(getApiErrorMessage(error, '重置密码失败'))
    } finally {
      resetLoadingId.value = null
    }
  }).catch(() => {})
}

function validatePassword(value: string) {
  return value && value.length >= 8 ? true : '密码长度最短为 8 位'
}

function handleDelete(row: AdminAccountListItem) {
  ElMessageBox.confirm(
    `确定要删除管理员 ${row.username}？删除后该账号将无法登录，且所有活跃会话立即失效。`,
    '确认删除',
    {
      confirmButtonText: '确认删除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    }
  ).then(async () => {
    try {
      await deleteAdmin(row.id)
      ElMessage.success('管理员删除成功')
      fetchAdmins()
    } catch (error: unknown) {
      console.error(error)
      ElMessage.error(getApiErrorMessage(error, '删除管理员失败'))
    }
  }).catch(() => {})
}

onMounted(() => {
  fetchAdmins()
})
</script>

<style scoped lang="scss">
.admins-container {
  padding: 24px;

  @media (max-width: 767px) {
    padding: 12px;
  }
}

.username-text {
  font-size: 14px;
  color: #303133;
  font-weight: 500;
}

.action-buttons {
  display: flex;
  align-items: center;
  gap: 8px;
}

.disabled-action {
  color: #c0c4cc;
}
</style>
