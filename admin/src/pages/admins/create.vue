<template>
  <admin-layout>
    <div class="create-container">
      <!-- Back button -->
      <div class="header-action-row">
        <el-button type="default" @click="goBack">
          ← 返回列表
        </el-button>
      </div>

      <!-- Main Form Card -->
      <div class="admin-card create-card">
        <h3 class="card-title-custom">创建管理员</h3>

        <el-form
          ref="formRef"
          :model="form"
          :rules="rules"
          label-position="top"
          class="create-form"
        >
          <!-- Username Input -->
          <el-form-item label="用户名" prop="username">
            <el-input
              v-model="form.username"
              placeholder="请输入用户名"
              class="custom-input"
              maxlength="20"
            />
          </el-form-item>

          <!-- Password Input -->
          <el-form-item label="初始密码" prop="password">
            <el-input
              v-model="form.password"
              type="password"
              show-password
              placeholder="请输入初始密码（最短 8 位）"
              class="custom-input"
            />
          </el-form-item>

          <!-- Confirm Password Input -->
          <el-form-item label="确认密码" prop="confirmPassword">
            <el-input
              v-model="form.confirmPassword"
              type="password"
              show-password
              placeholder="请再次输入密码以确认"
              class="custom-input"
            />
          </el-form-item>

          <!-- Action Buttons -->
          <el-form-item class="form-actions">
            <el-button @click="goBack">取消</el-button>
            <el-button
              type="primary"
              :loading="submitting"
              @click="submitForm"
            >
              创建
            </el-button>
          </el-form-item>
        </el-form>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FormInstance } from 'element-plus'
import AdminLayout from '@/components/admin-layout.vue'
import { createAdmin } from '@/api/admins'
import { getApiErrorMessage } from '@/api/client'
import { logApiError } from '@/utils/safe-api-error'

const router = useRouter()
const formRef = ref<FormInstance>()
const submitting = ref(false)
type ValidationCallback = (error?: Error) => void

const form = reactive({
  username: '',
  password: '',
  confirmPassword: '',
})

const validateUsername = (_rule: unknown, value: string, callback: ValidationCallback) => {
  if (!value) {
    callback(new Error('请输入用户名'))
  } else if (value.length < 3 || value.length > 20) {
    callback(new Error('用户名长度必须在 3 到 20 个字符之间'))
  } else if (!/^[a-zA-Z0-9_]+$/.test(value)) {
    callback(new Error('用户名只能包含字母、数字和下划线'))
  } else {
    callback()
  }
}

const validatePassword = (_rule: unknown, value: string, callback: ValidationCallback) => {
  if (!value) {
    callback(new Error('请输入初始密码'))
  } else if (value.length < 8) {
    callback(new Error('密码长度最短为 8 位'))
  } else {
    if (form.confirmPassword !== '') {
      if (formRef.value) {
        formRef.value.validateField('confirmPassword')
      }
    }
    callback()
  }
}

const validateConfirmPassword = (_rule: unknown, value: string, callback: ValidationCallback) => {
  if (!value) {
    callback(new Error('请再次输入密码以确认'))
  } else if (value !== form.password) {
    callback(new Error('两次输入的密码不一致'))
  } else {
    callback()
  }
}

const rules = reactive({
  username: [{ validator: validateUsername, trigger: 'blur', required: true }],
  password: [{ validator: validatePassword, trigger: 'blur', required: true }],
  confirmPassword: [{ validator: validateConfirmPassword, trigger: 'blur', required: true }],
})

function goBack() {
  router.push('/admins')
}

async function submitForm() {
  if (!formRef.value) return

  try {
    const valid = await formRef.value.validate()
    if (valid) {
      submitting.value = true
      try {
        await createAdmin({
          username: form.username,
          password: form.password,
        })
        ElMessage.success('管理员创建成功')
        router.push('/admins')
      } catch (error: unknown) {
        logApiError({ context: 'admins.create', error, fallback: '创建管理员失败' })
        ElMessage.error(getApiErrorMessage(error, '创建管理员失败'))
      } finally {
        submitting.value = false
      }
    }
  } catch (error) {
    // Validation failed
    console.log('表单校验失败', error)
  }
}
</script>

<style scoped lang="scss">
.create-container {
  padding: 24px;

  @media (max-width: 767px) {
    padding: 12px;
  }
}

.header-action-row {
  margin-bottom: 20px;
}

.create-card {
  background-color: #ffffff;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.06);
  max-width: 520px;
  padding: 32px;

  @media (max-width: 767px) {
    max-width: 100%;
    padding: 20px 16px;
  }
}

.card-title-custom {
  margin: 0 0 24px 0;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
  padding-bottom: 16px;
  border-bottom: 1px solid #ebeef5;
}

.create-form {
  :deep(.el-form-item__label) {
    font-size: 14px;
    color: #606266;
    font-weight: 500;
    padding-bottom: 8px;
  }
}

.custom-input {
  :deep(.el-input__wrapper) {
    height: 40px;
  }
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 32px;
  margin-bottom: 0;

  :deep(.el-form-item__content) {
    justify-content: flex-end;
    gap: 12px;
  }
}
</style>
