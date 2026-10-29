<template>
  <div class="layout-container">
    <!-- Desktop Fixed Sidebar -->
    <aside v-if="isDesktop" :class="['sidebar-aside', { 'is-collapsed': isCollapsed }]">
      <div class="logo-area">
        <div class="logo-icon-box">
          <div class="red-block-logo" />
        </div>
        <span class="logo-text">{{ siteConfig.adminTitle }}</span>
      </div>

      <admin-navigation
        :active-menu="activeMenu"
        :collapsed="isCollapsed"
        :default-openeds="defaultOpeneds"
        :is-super-admin="isSuperAdmin"
        appearance="sidebar"
        @select="handleMenuSelect"
      />

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
        <span class="logo-text">{{ siteConfig.adminTitle }}</span>
      </div>

      <admin-navigation
        :active-menu="activeMenu"
        :default-openeds="defaultOpeneds"
        :is-super-admin="isSuperAdmin"
        appearance="drawer"
        @select="handleDrawerMenuSelect"
      />
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
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Expand, Fold, Menu } from '@element-plus/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import { logout } from '@/api/auth'
import AdminNavigation from '@/components/admin-navigation.vue'
import { useAdminStore } from '@/stores/admin'
import { logApiError } from '@/utils/safe-api-error'
import { siteConfig } from '@/config/site-config'

const BREAKPOINT_TABLET = 1024
const ACTIVE_MENU_PREFIXES = [
  ['/help-requests', '/help-requests'],
  ['/rescue-sessions', '/rescue-sessions'],
  ['/users', '/users'],
  ['/devices', '/devices'],
  ['/rescuers', '/rescuers'],
  ['/volunteer-applications', '/volunteer-applications'],
  ['/school-profiles', '/school-profiles'],
  ['/school-bindings', '/school-bindings'],
  ['/responder-claims', '/responder-claims'],
  ['/admins', '/admins'],
  ['/classroom/quick-actions', '/classroom/quick-actions'],
  ['/classroom/courses', '/classroom/courses'],
  ['/classroom', '/classroom/summary'],
] as const

const route = useRoute()
const router = useRouter()
const store = useAdminStore()
const logoutLoading = ref(false)
const drawerVisible = ref(false)

const windowWidth = ref(typeof window !== 'undefined' ? window.innerWidth : 1200)
const isDesktop = computed(() => windowWidth.value >= BREAKPOINT_TABLET)

const isCollapsed = ref(localStorage.getItem('sidebar_collapsed') === 'true')

const activeMenu = computed(() => {
  const match = ACTIVE_MENU_PREFIXES.find(([prefix]) => route.path.startsWith(prefix))
  return match?.[1] ?? route.path
})
const defaultOpeneds = computed(() => (route.path.startsWith('/classroom') ? ['classroom'] : []))
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

async function handleMenuSelect(index: string) {
  if (!index.startsWith('/') || index === route.path) return
  await router.push(index)
}

async function handleDrawerMenuSelect(index: string) {
  await handleMenuSelect(index)
  drawerVisible.value = false
}

async function handleLogout() {
  logoutLoading.value = true
  try {
    await logout()
    store.clearSession()
    await router.replace('/login')
  } catch (error) {
    logApiError({
      context: 'auth.logout',
      error,
      fallback: '登出失败，请稍后重试',
    })
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

<style scoped lang="scss" src="./admin-layout.scss"></style>
