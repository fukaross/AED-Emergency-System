<template>
  <div class="layout-container">
    <!-- Fixed Sidebar -->
    <aside :class="['sidebar-aside', { 'is-collapsed': isCollapsed }]">
      <!-- Logo area -->
      <div class="logo-area">
        <div class="logo-icon-box">
          <div class="red-block-logo" />
        </div>
        <span class="logo-text">AED 管理端</span>
      </div>

      <!-- Nav Menu -->
      <el-menu
        :default-active="activeMenu"
        :router="true"
        :collapse="isCollapsed"
        background-color="#1d2b3a"
        text-color="#bfcbd9"
        active-text-color="#ffffff"
        class="sidebar-menu"
      >
        <el-menu-item index="/dashboard">
          <el-icon><Odometer /></el-icon>
          <template #title>仪表盘</template>
        </el-menu-item>
        <el-menu-item index="/help-requests">
          <el-icon><Warning /></el-icon>
          <template #title>求助管理</template>
        </el-menu-item>
        <el-menu-item index="/users">
          <el-icon><User /></el-icon>
          <template #title>用户管理</template>
        </el-menu-item>
        <el-menu-item v-if="isSuperAdmin" index="/admins">
          <el-icon><Setting /></el-icon>
          <template #title>管理员</template>
        </el-menu-item>
        <el-menu-item v-if="isSuperAdmin" index="/audit-logs">
          <el-icon><Document /></el-icon>
          <template #title>操作日志</template>
        </el-menu-item>
      </el-menu>

      <!-- Collapse Toggle Button at sidebar bottom -->
      <div class="collapse-btn" @click="toggleCollapse">
        <el-icon>
          <Expand v-if="isCollapsed" />
          <Fold v-else />
        </el-icon>
      </div>
    </aside>

    <!-- Main Container -->
    <div :class="['main-wrapper', { 'is-collapsed-margin': isCollapsed }]">
      <!-- Sticky Header -->
      <header class="header-navbar">
        <div class="header-left">
          <span class="page-title">{{ pageTitle }}</span>
        </div>
        <div class="header-right">
          <span class="username-display">{{ username }}</span>
          <router-link to="/sessions" class="sessions-link">Session管理</router-link>
          <el-button text type="primary" :loading="logoutLoading" @click="handleLogout">登出</el-button>
        </div>
      </header>

      <!-- Main Slot Content -->
      <main class="content-viewport">
        <slot />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useRoute, useRouter } from 'vue-router'
import { logout } from '@/api/auth'
import { useAdminStore } from '@/stores/admin'
import {
  Odometer,
  Warning,
  User,
  Setting,
  Document,
  Fold,
  Expand
} from '@element-plus/icons-vue'

const route = useRoute()
const router = useRouter()
const store = useAdminStore()
const logoutLoading = ref(false)

// Collapse state managed in localStorage
const isCollapsed = ref(localStorage.getItem('sidebar_collapsed') === 'true')

const activeMenu = computed(() => {
  const path = route.path
  if (path.startsWith('/help-requests')) return '/help-requests'
  if (path.startsWith('/users')) return '/users'
  if (path.startsWith('/admins')) return '/admins'
  return path
})
const username = computed(() => store.username)
const isSuperAdmin = computed(() => store.isSuperAdmin)
const pageTitle = computed(() => (route.meta.title as string) || '')

function toggleCollapse() {
  isCollapsed.value = !isCollapsed.value
  localStorage.setItem('sidebar_collapsed', String(isCollapsed.value))
}

async function handleLogout() {
  logoutLoading.value = true
  try {
    await logout()
    store.clearSession()
    await router.replace('/login')
  } catch (error) {
    console.error(error)
    ElMessage.error('登出失败，请稍后重试')
  } finally {
    logoutLoading.value = false
  }
}
</script>

<style scoped lang="scss">
.layout-container {
  display: flex;
  min-height: 100vh;
  position: relative;
}

// Fixed Sidebar Styling
.sidebar-aside {
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  width: 220px;
  background-color: #1d2b3a;
  display: flex;
  flex-direction: column;
  z-index: 1000;
  transition: width 0.3s ease;
  overflow: hidden;

  &.is-collapsed {
    width: 64px;

    .logo-text {
      opacity: 0;
      width: 0;
      pointer-events: none;
    }
  }
}

// Logo Area inside sidebar
.logo-area {
  height: 60px;
  display: flex;
  align-items: center;
  padding: 0 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  flex-shrink: 0;
  overflow: hidden;
}

.logo-icon-box {
  width: 32px;
  height: 32px;
  display: flex;
  justify-content: center;
  align-items: center;
  flex-shrink: 0;
}

.red-block-logo {
  width: 20px;
  height: 20px;
  background-color: #f56c6c;
  border-radius: 4px;
}

.logo-text {
  font-size: 16px;
  font-weight: 600;
  color: #ffffff;
  margin-left: 12px;
  white-space: nowrap;
  transition: opacity 0.2s ease, width 0.2s ease;
  opacity: 1;
}

// Navigation menu styling
.sidebar-menu {
  border-right: none;
  flex: 1;
  overflow-y: auto;

  &:not(.el-menu--collapse) {
    width: 220px;
  }

  :deep(.el-menu-item) {
    height: 48px;
    line-height: 48px;
    padding-left: 20px !important;
    font-size: 14px;
    color: #bfcbd9;

    .el-icon {
      font-size: 18px;
      margin-right: 12px;
      color: #bfcbd9;
    }

    &:hover {
      background-color: #243447 !important;
      color: #e0e6ed !important;

      .el-icon {
        color: #e0e6ed !important;
      }
    }

    &.is-active {
      background-color: #2b3f54 !important;
      color: #ffffff !important;
      position: relative;

      .el-icon {
        color: #ffffff !important;
      }

      &::before {
        content: '';
        position: absolute;
        left: 0;
        top: 0;
        bottom: 0;
        width: 3px;
        background-color: #409eff;
      }
    }
  }

  // Collapsed specific indicator settings
  &.el-menu--collapse {
    :deep(.el-menu-item) {
      padding-left: 0 !important;
      display: flex;
      justify-content: center;
      align-items: center;

      .el-icon {
        margin-right: 0;
      }

      &::before {
        display: none !important;
      }
    }
  }
}

// Sidebar toggle button at bottom
.collapse-btn {
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
  color: #bfcbd9;
  cursor: pointer;
  flex-shrink: 0;
  transition: color 0.2s ease;

  &:hover {
    color: #ffffff;
    background-color: #243447;
  }

  .el-icon {
    font-size: 18px;
  }
}

// Right Main Wrapper Layout
.main-wrapper {
  flex: 1;
  margin-left: 220px;
  transition: margin-left 0.3s ease;
  display: flex;
  flex-direction: column;
  min-width: 980px;

  &.is-collapsed-margin {
    margin-left: 64px;
    min-width: 1136px;
  }
}

// Sticky Header Styling
.header-navbar {
  position: sticky;
  top: 0;
  z-index: 99;
  height: 60px;
  background-color: #ffffff;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.04);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  flex-shrink: 0;
}

.page-title {
  font-size: 16px;
  font-weight: 500;
  color: #303133;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 16px;
}

.username-display {
  font-size: 14px;
  color: #606266;
}

.sessions-link {
  font-size: 14px;
  color: #409eff;
  text-decoration: none;
  cursor: pointer;

  &:hover {
    color: #73b8ff;
    text-decoration: underline;
  }
}

// Content area container wrapper
.content-viewport {
  flex: 1;
  background-color: #f0f2f5;
}
</style>
