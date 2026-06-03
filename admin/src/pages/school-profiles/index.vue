<template>
  <admin-layout>
    <div class="page-content-padding">
      <div class="admin-card no-padding">
        <div class="card-header-bar">
          <h3 class="card-header-title">学校档案</h3>
          <el-button type="primary" @click="openCreate">新建档案</el-button>
        </div>

        <div class="filter-bar">
          <el-select v-model="personType" placeholder="类型" clearable style="width: 130px" @change="reloadFirst">
            <el-option label="全部" value="" />
            <el-option label="学生" value="student" />
            <el-option label="教师" value="teacher" />
            <el-option label="职工" value="staff" />
          </el-select>
          <el-input v-model="keyword" placeholder="搜索姓名/学院/学号" clearable style="width: 240px" @keyup.enter="reloadFirst" @clear="reloadFirst" />
          <el-button type="primary" @click="reloadFirst">查询</el-button>
        </div>

        <div class="table-responsive">
          <el-table v-loading="loading" :data="rows" class="data-table" stripe>
            <el-table-column prop="profileId" label="ID" width="80" />
            <el-table-column prop="name" label="姓名" width="100" />
            <el-table-column label="类型" width="80">
              <template #default="{ row }">{{ personTypeLabel(row.personType) }}</template>
            </el-table-column>
            <el-table-column prop="college" label="学院" min-width="140" />
            <el-table-column prop="studentId" label="学号" width="130" />
            <el-table-column prop="staffId" label="工号" width="130" />
            <el-table-column label="状态" width="90">
              <template #default="{ row }"><status-tag :status="row.status" category="schoolProfile" /></template>
            </el-table-column>
            <el-table-column label="操作" width="150" fixed="right">
              <template #default="{ row }">
                <el-button text type="primary" size="small" @click="router.push(`/school-profiles/${row.profileId}`)">详情</el-button>
                <el-button text type="primary" size="small" @click="openEdit(row.profileId)">编辑</el-button>
              </template>
            </el-table-column>
            <template #empty><el-empty description="暂无学校档案" /></template>
          </el-table>
        </div>

        <div class="pagination-container">
          <el-pagination v-model:current-page="page" :page-size="pageSize" :total="total" small background layout="total, prev, pager, next" @current-change="load" />
        </div>
      </div>
    </div>

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑档案' : '新建档案'" width="520px">
      <el-form :model="form" label-width="80px">
        <el-form-item label="类型"><el-select v-model="form.personType"><el-option label="学生" value="student" /><el-option label="教师" value="teacher" /><el-option label="职工" value="staff" /></el-select></el-form-item>
        <el-form-item label="姓名"><el-input v-model="form.name" maxlength="40" /></el-form-item>
        <el-form-item label="学院"><el-input v-model="form.college" maxlength="40" /></el-form-item>
        <el-form-item label="班级"><el-input v-model="form.className" maxlength="40" /></el-form-item>
        <el-form-item label="学号"><el-input v-model="form.studentId" maxlength="32" /></el-form-item>
        <el-form-item label="工号"><el-input v-model="form.staffId" maxlength="32" /></el-form-item>
        <el-form-item label="手机号"><el-input v-model="form.phone" maxlength="11" /></el-form-item>
        <el-form-item label="状态"><el-select v-model="form.status"><el-option label="有效" value="active" /><el-option label="停用" value="inactive" /></el-select></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </admin-layout>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { createSchoolProfile, getSchoolProfileDetail, getSchoolProfiles, updateSchoolProfile } from '@/api/user-identity'
import { getApiErrorMessage } from '@/api/client'
import type { AdminSchoolProfileItem, SchoolPersonType, SchoolProfilePayload } from '@/types/user-identity'

const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const rows = ref<AdminSchoolProfileItem[]>([])
const page = ref(1)
const pageSize = 20
const total = ref(0)
const personType = ref('')
const keyword = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SchoolProfilePayload>(emptyForm())
const router = useRouter()

function emptyForm(): SchoolProfilePayload {
  return { personType: 'student', name: '', college: '', className: '', studentId: '', staffId: '', phone: '', status: 'active' }
}

async function load() {
  loading.value = true
  try {
    const res = await getSchoolProfiles({ page: page.value, pageSize, personType: personType.value as SchoolPersonType | undefined, keyword: keyword.value || undefined })
    rows.value = res.data.data.items
    total.value = res.data.data.total
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '加载学校档案失败'))
  } finally {
    loading.value = false
  }
}

function reloadFirst() {
  page.value = 1
  load()
}

function openCreate() {
  Object.assign(form, emptyForm())
  editingId.value = null
  dialogVisible.value = true
}

async function openEdit(id: string) {
  try {
    const res = await getSchoolProfileDetail(id)
    Object.assign(form, res.data.data)
    editingId.value = id
    dialogVisible.value = true
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '加载档案失败'))
  }
}

async function save() {
  saving.value = true
  try {
    if (editingId.value) await updateSchoolProfile(editingId.value, form)
    else await createSchoolProfile(form)
    dialogVisible.value = false
    ElMessage.success('已保存')
    await load()
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '保存失败'))
  } finally {
    saving.value = false
  }
}

function personTypeLabel(value: string) {
  return value === 'teacher' ? '教师' : value === 'staff' ? '职工' : '学生'
}

onMounted(load)
</script>
