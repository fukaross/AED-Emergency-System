<template>
  <div class="layout-container">
    <!-- Desktop Fixed Sidebar -->
    <aside v-if="isDesktop" :class="['sidebar-aside', { 'is-collapsed': isCollapsed }]">
      <div class="logo-area">
        <div class="logo-icon-box">
          <div class="red-block-logo" />
        </div>
        <span class="logo-text">AED 管理端</span>
      </div>

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

      <div class="collapse-btn" @click="toggleCollapse">
        <el-icon>
          <Expand v-if="isCollapsed" />
          <Fold v-else />
        </el-icon>
      </div>
    </aside>

    <!-- Mobile/Tablet Drawer -->
    <el-drawer
      v-if="!isDesktop"
      v-model="drawerVisible"
      direction="ltr"
      size="220px"
      :with-header="false"
    >
      <div class="drawer-logo-area">
        <div class="logo-icon-box">
          <div class="red-block-logo" />
        </div>
        <span class="logo-text">AED 管理端</span>
      </div>

      <el-menu
        :default-active="activeMenu"
        :router="true"
        background-color="#ffffff"
        text-color="#303133"
        active-text-color="#409eff"
        class="drawer-menu"
        @select="closeDrawer"
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
        <el-menu-item index="/sessions">
          <el-icon><Monitor /></el-icon>
          <template #title>会话管理</template>
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
    </el-drawer>

    <!-- Main Container -->
    <div :class="mainWrapperClass">
      <header class="header-navbar">
        <div class="header-left">
          <el-button
            v-if="!isDesktop"
            :icon="Menu"
            text
            class="hamburger-btn"
            @click="drawerVisible = true"
          />
          <span class="page-title">{{ pageTitle }}</span>
        </div>
        <div class="header-right">
          <span class="username-display hide-on-mobile">{{ username }}</span>
          <router-link to="/sessions" class="sessions-link hide-on-mobile">Session管理</router-link>
          <el-button text type="primary" :loading="logoutLoading" @click="handleLogout">登出</el-button>
        </div>
      </header>

      <main class="content-viewport">
        <slot />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
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
  Expand,
  Menu,
  Monitor
} from '@element-plus/icons-vue'

const BREAKPOINT_TABLET = 1024

const route = useRoute()
const router = useRouter()
const store = useAdminStore()
const logoutLoading = ref(false)
const drawerVisible = ref(false)

const windowWidth = ref(typeof window !== 'undefined' ? window.innerWidth : 1200)
const isDesktop = computed(() => windowWidth.value >= BREAKPOINT_TABLET)

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

const mainWrapperClass = computed(() => ({
  'main-wrapper': true,
  'is-collapsed-margin': isDesktop.value && isCollapsed.value,
  'is-full-width': !isDesktop.value,
}))

function toggleCollapse() {
  isCollapsed.value = !isCollapsed.value
  localStorage.setItem('sidebar_collapsed', String(isCollapsed.value))
}

function closeDrawer() {
  drawerVisible.value = false
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

function onResize() {
  windowWidth.value = window.innerWidth
}

onMounted(() => {
  window.addEventListener('resize', onResize)
})

onUnmounted(() => {
  window.removeEventListener('resize', onResize)
})
</script>

<style scoped lang="scss">
$sidebar-w: 220px;
$sidebar-collapsed-w: 64px;
$header-h: 60px;

.layout-container {
  display: flex;
  min-height: 100vh;
  position: relative;
}

.sidebar-aside {
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  width: $sidebar-w;
  background-color: #1d2b3a;
  display: flex;
  flex-direction: column;
  z-index: 1000;
  transition: width 0.3s ease;
  overflow: hidden;

  &.is-collapsed {
    width: $sidebar-collapsed-w;

    .logo-text {
      opacity: 0;
      width: 0;
      pointer-events: none;
    }
  }
}

.logo-area {
  height: $header-h;
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

.drawer-logo-area {
  height: 56px;
  display: flex;
  align-items: center;
  padding: 0 16px;
  border-bottom: 1px solid #ebeef5;
  flex-shrink: 0;

  .logo-text {
    color: #303133;
    opacity: 1;
  }
}

.sidebar-menu {
  border-right: none;
  flex: 1;
  overflow-y: auto;

  &:not(.el-menu--collapse) {
    width: $sidebar-w;
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

.drawer-menu {
  border-right: none;

  :deep(.el-menu-item) {
    height: 48px;
    line-height: 48px;
    font-size: 14px;

    .el-icon {
      font-size: 18px;
    }
  }
}

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

.main-wrapper {
  flex: 1;
  margin-left: $sidebar-w;
  transition: margin-left 0.3s ease;
  display: flex;
  flex-direction: column;
  min-height: 100vh;

  &.is-collapsed-margin {
    margin-left: $sidebar-collapsed-w;
  }

  &.is-full-width {
    margin-left: 0;
  }
}

.hamburger-btn {
  font-size: 20px;
  padding: 4px;
  margin-right: 8px;
  color: #303133;
}

.header-navbar {
  position: sticky;
  top: 0;
  z-index: 99;
  height: $header-h;
  background-color: #ffffff;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.04);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  flex-shrink: 0;

  @media (max-width: 767px) {
    padding: 0 12px;
  }
}

.header-left {
  display: flex;
  align-items: center;
  min-width: 0;
}

.page-title {
  font-size: 16px;
  font-weight: 500;
  color: #303133;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;

  @media (max-width: 767px) {
    font-size: 14px;
  }
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-shrink: 0;

  @media (max-width: 767px) {
    gap: 4px;
  }
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

.content-viewport {
  flex: 1;
  background-color: #f0f2f5;
}

@media (max-width: 767px) {
  .hide-on-mobile {
    display: none !important;
  }
}
</style>
