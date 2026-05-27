<template>
  <div class="login-wrapper">
    <div class="login-card">
      <!-- Logo Area -->
      <div class="logo-section">
        <div class="logo-box">
          <div class="red-logo-block" />
        </div>
        <h2 class="main-title">AED 管理端</h2>
        <p class="subtitle">REDACTED AED 急救管理系统</p>
      </div>

      <!-- Info Alert for Bootstrap Mode -->
      <el-alert
        v-if="isBootstrapMode"
        title="系统尚未初始化，请使用管理密钥创建首个超级管理员"
        type="warning"
        :closable="false"
        show-icon
        class="bootstrap-alert"
      />

      <!-- Form Area -->
      <el-form
        ref="loginFormRef"
        :model="form"
        :rules="rules"
        label-position="top"
        @keyup.enter="handleSubmit"
      >
        <el-form-item prop="username">
          <el-input
            v-model="form.username"
            placeholder="请输入用户名"
            :prefix-icon="User"
            class="login-input"
          />
        </el-form-item>

        <el-form-item prop="password">
          <el-input
            v-model="form.password"
            type="password"
            placeholder="请输入密码"
            :prefix-icon="Lock"
            show-password
            class="login-input"
          />
        </el-form-item>

        <!-- Admin Key Input (Only visible in Bootstrap mode) -->
        <el-form-item v-if="isBootstrapMode" prop="adminKey">
          <el-input
            v-model="form.adminKey"
            type="password"
            placeholder="请输入管理密钥(Admin Key)"
            :prefix-icon="Key"
            show-password
            class="login-input"
          />
        </el-form-item>

        <el-form-item class="submit-item">
          <el-button
            type="primary"
            :loading="loading"
            class="submit-button"
            @click="handleSubmit"
          >
            {{ isBootstrapMode ? '初始化并登录' : '登录' }}
          </el-button>
        </el-form-item>
      </el-form>

      <el-button
        link
        type="primary"
        class="mode-switch"
        @click="toggleBootstrapMode"
      >
        {{ isBootstrapMode ? '返回登录' : '初始化超级管理员' }}
      </el-button>

      <!-- Error Alert -->
      <el-alert
        v-if="errorMessage"
        :title="errorMessage"
        type="error"
        :closable="false"
        show-icon
        class="error-alert"
      />
    </div>

    <div class="footer-copyright">
      REDACTED AED 急救系统
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import type { FormInstance, FormRules } from 'element-plus'
import { User, Lock, Key } from '@element-plus/icons-vue'
import { login, bootstrap } from '@/api/auth'
import { useAdminStore } from '@/stores/admin'
import {
  getApiErrorCode,
  getApiErrorMessage,
  getApiErrorStatus,
  isNetworkApiError,
} from '@/api/client'

const router = useRouter()
const store = useAdminStore()

const loginFormRef = ref<FormInstance>()
const loading = ref(false)
const isBootstrapMode = ref(false)
const errorMessage = ref('')

const form = reactive({
  username: '',
  password: '',
  adminKey: '',
})

onMounted(async () => {
  await detectBootstrapRequired()
})

// Validation rules
const rules = computed<FormRules>(() => {
  const baseRules: FormRules = {
    username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
    password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
  }

  if (isBootstrapMode.value) {
    baseRules.adminKey = [{ required: true, message: '请输入管理密钥', trigger: 'blur' }]
  }

  return baseRules
})

// Handle Form Submission (handles both regular login and bootstrap registration)
async function handleSubmit() {
  if (!loginFormRef.value) return
  errorMessage.value = ''

  await loginFormRef.value.validate(async (valid) => {
    if (!valid) return

    loading.value = true
    try {
      if (isBootstrapMode.value) {
        // Bootstrap flow: Register first super admin
        await bootstrap(
          { username: form.username, password: form.password },
          { adminKey: form.adminKey }
        )
      }

      // Execute login (if bootstrap succeeded, we automatically login)
      const response = await login({
        username: form.username,
        password: form.password,
      })

      // Set Pinia & LocalStorage session state
      store.setSession(response.data.data)
      // Redirect to dashboard
      await router.replace('/dashboard')
    } catch (error: unknown) {
      console.error(error)
      handleErrorResponse(error)
    } finally {
      loading.value = false
    }
  })
}

// Map server & network errors to user-friendly messages
function handleErrorResponse(error: unknown) {
  const status = getApiErrorStatus(error)
  const errorCode = getApiErrorCode(error)

  if (errorCode === 'SYSTEM_NOT_INITIALIZED') {
    isBootstrapMode.value = true
    errorMessage.value = '系统尚未初始化，请使用管理密钥创建首个超级管理员'
    return
  }

  if (errorCode === 'SESSION_LIMIT_REACHED') {
    errorMessage.value = '已达到最大登录设备数，请使用已登录设备管理会话，或联系超级管理员'
    return
  }

  if (errorCode === 'RATE_LIMITED' || errorCode === 3003 || status === 429) {
    errorMessage.value = '登录尝试过于频繁，请 15 分钟后重试'
    return
  }

  // Handle invalid credentials
  if (status === 400 || status === 401) {
    if (isBootstrapMode.value) {
      errorMessage.value = getApiErrorMessage(error, '管理密钥错误或配置失败')
    } else {
      errorMessage.value = '用户名或密码错误'
    }
    return
  }

  // Connectivity or server exceptions
  if (isNetworkApiError(error) || !status) {
    errorMessage.value = '网络连接失败，请检查网络后重试'
  } else {
    errorMessage.value = getApiErrorMessage(error, '服务器内部错误，请稍后重试')
  }
}

async function detectBootstrapRequired() {
  try {
    await login({
      username: '__bootstrap_probe__',
      password: '__bootstrap_probe__',
    })
  } catch (error: unknown) {
    if (getApiErrorCode(error) === 'SYSTEM_NOT_INITIALIZED') {
      isBootstrapMode.value = true
    }
  }
}

function toggleBootstrapMode() {
  isBootstrapMode.value = !isBootstrapMode.value
  errorMessage.value = ''
}
</script>

<style scoped lang="scss">
.login-wrapper {
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  background-color: #f0f2f5;
  padding: 20px;
}

.login-card {
  width: 400px;
  max-width: calc(100% - 32px);
  background-color: #ffffff;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.08);
  padding: 40px;
  margin-bottom: 24px;

  @media (max-width: 767px) {
    padding: 24px 20px;
  }
}

.logo-section {
  display: flex;
  flex-direction: column;
  align-items: center;
  margin-bottom: 32px;
  text-align: center;
}

.logo-box {
  width: 48px;
  height: 48px;
  display: flex;
  justify-content: center;
  align-items: center;
  margin-bottom: 12px;
}

.red-logo-block {
  width: 32px;
  height: 32px;
  background-color: #f56c6c;
  border-radius: 6px;
}

.main-title {
  font-size: 18px;
  font-weight: 600;
  color: #303133;
  margin: 0 0 6px 0;
}

.subtitle {
  font-size: 13px;
  color: #909399;
  margin: 0;
}

.bootstrap-alert {
  margin-bottom: 20px;
}

.login-input {
  --el-input-height: 40px;
  --el-input-border-radius: 4px;
}

.submit-item {
  margin-top: 28px;
  margin-bottom: 0;
}

.submit-button {
  width: 100%;
  height: 40px;
  font-size: 14px;
}

.mode-switch {
  display: block;
  margin: 16px auto 0;
}

.error-alert {
  margin-top: 16px;
}

.footer-copyright {
  font-size: 12px;
  color: #c0c4cc;
  text-align: center;
}
</style>
