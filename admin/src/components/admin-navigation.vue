<template>
  <el-menu
    :default-active="activeMenu"
    :default-openeds="defaultOpeneds"
    :collapse="appearance === 'sidebar' && collapsed"
    :background-color="menuTheme.backgroundColor"
    :text-color="menuTheme.textColor"
    :active-text-color="menuTheme.activeTextColor"
    :class="['admin-navigation', `is-${appearance}`]"
    @select="handleSelect"
  >
    <template v-for="item in visibleItems" :key="item.index">
      <el-sub-menu v-if="item.children" :index="item.index">
        <template #title>
          <el-icon><component :is="item.icon" /></el-icon>
          <span>{{ item.label }}</span>
        </template>
        <el-menu-item v-for="child in item.children" :key="child.index" :index="child.index">
          {{ child.label }}
        </el-menu-item>
      </el-sub-menu>
      <el-menu-item v-else :index="item.index">
        <el-icon><component :is="item.icon" /></el-icon>
        <template #title>{{ item.label }}</template>
      </el-menu-item>
    </template>
  </el-menu>
</template>

<script setup lang="ts">
import { computed, type Component } from 'vue'
import {
  Avatar,
  Checked,
  Document,
  Finished,
  Link,
  MapLocation,
  Monitor,
  Odometer,
  Place,
  Postcard,
  Reading,
  Setting,
  User,
  Warning,
} from '@element-plus/icons-vue'

type NavigationAppearance = 'sidebar' | 'drawer'

interface NavigationChild {
  readonly index: string
  readonly label: string
}

interface NavigationItem {
  readonly index: string
  readonly label: string
  readonly icon: Component
  readonly children?: readonly NavigationChild[]
  readonly drawerOnly?: boolean
  readonly superAdminOnly?: boolean
}

const NAVIGATION_ITEMS: readonly NavigationItem[] = [
  { index: '/dashboard', label: '仪表盘', icon: Odometer },
  { index: '/help-requests', label: '求助管理', icon: Warning },
  { index: '/rescue-sessions', label: '急救会话', icon: MapLocation },
  { index: '/users', label: '用户管理', icon: User },
  { index: '/devices', label: 'AED 管理', icon: Place },
  { index: '/rescuers', label: '急救员管理', icon: Avatar },
  { index: '/volunteer-applications', label: '志愿者审核', icon: Checked },
  { index: '/school-profiles', label: '学校档案', icon: Postcard },
  { index: '/school-bindings', label: '学校绑定审核', icon: Finished },
  { index: '/responder-claims', label: '急救员认领审核', icon: Link },
  { index: '/sessions', label: '会话管理', icon: Monitor, drawerOnly: true },
  { index: '/admins', label: '管理员', icon: Setting, superAdminOnly: true },
  { index: '/audit-logs', label: '操作日志', icon: Document, superAdminOnly: true },
  {
    index: 'classroom',
    label: '急救课堂',
    icon: Reading,
    children: [
      { index: '/classroom/summary', label: '统计横幅' },
      { index: '/classroom/quick-actions', label: '关键步骤' },
      { index: '/classroom/courses', label: '课程管理' },
    ],
  },
]

const MENU_THEMES = {
  sidebar: {
    backgroundColor: '#1d2b3a',
    textColor: '#bfcbd9',
    activeTextColor: '#ffffff',
  },
  drawer: {
    backgroundColor: '#ffffff',
    textColor: '#303133',
    activeTextColor: '#409eff',
  },
} as const

const props = defineProps<{
  activeMenu: string
  appearance: NavigationAppearance
  collapsed?: boolean
  defaultOpeneds: string[]
  isSuperAdmin: boolean
}>()

const emit = defineEmits<{
  select: [index: string]
}>()

const menuTheme = computed(() => MENU_THEMES[props.appearance])
const visibleItems = computed(() =>
  NAVIGATION_ITEMS.filter((item) => {
    if (item.drawerOnly && props.appearance !== 'drawer') return false
    if (item.superAdminOnly && !props.isSuperAdmin) return false
    return true
  }),
)

function handleSelect(index: string) {
  emit('select', index)
}
</script>

<style scoped lang="scss" src="./admin-navigation.scss"></style>
