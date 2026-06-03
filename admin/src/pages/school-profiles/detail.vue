<template>
  <admin-layout>
    <div class="page-content-padding">
      <el-button text class="back-link" @click="router.push('/school-profiles')">返回列表</el-button>
      <div v-loading="loading" class="admin-card">
        <div class="card-header-bar">
          <h3 class="card-header-title">学校档案详情</h3>
          <status-tag v-if="detail" :status="detail.status" category="schoolProfile" />
        </div>
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="ID">{{ detail.profileId }}</el-descriptions-item>
          <el-descriptions-item label="类型">{{ personTypeLabel(detail.personType) }}</el-descriptions-item>
          <el-descriptions-item label="姓名">{{ detail.name }}</el-descriptions-item>
          <el-descriptions-item label="学院">{{ detail.college }}</el-descriptions-item>
          <el-descriptions-item label="班级">{{ detail.className || '-' }}</el-descriptions-item>
          <el-descriptions-item label="学号">{{ detail.studentId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="工号">{{ detail.staffId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="手机号">{{ detail.phone || '-' }}</el-descriptions-item>
        </el-descriptions>
        <el-empty v-else-if="!loading" description="档案不存在" />
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getSchoolProfileDetail } from '@/api/user-identity'
import { getApiErrorMessage } from '@/api/client'
import type { AdminSchoolProfileDetail } from '@/types/user-identity'

const route = useRoute()
const router = useRouter()
const detail = ref<AdminSchoolProfileDetail | null>(null)
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    const res = await getSchoolProfileDetail(route.params.id as string)
    detail.value = res.data.data
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '加载档案失败'))
  } finally {
    loading.value = false
  }
}

function personTypeLabel(value: string) {
  return value === 'teacher' ? '教师' : value === 'staff' ? '职工' : '学生'
}

onMounted(load)
</script>

<style scoped>
.back-link { margin-bottom: 16px; }
</style>
