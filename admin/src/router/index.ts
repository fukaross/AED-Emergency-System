import { createRouter, createWebHashHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import { useAdminStore } from '@/stores/admin'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/pages/login/index.vue'),
    meta: { title: '登录' },
  },
  {
    path: '/sessions',
    name: 'sessions',
    component: () => import('@/pages/sessions/index.vue'),
    meta: { title: '会话管理', requiresAuth: true },
  },
  {
    path: '/dashboard',
    name: 'dashboard',
    component: () => import('@/pages/dashboard/index.vue'),
    meta: { title: '仪表盘', requiresAuth: true },
  },
  {
    path: '/help-requests',
    name: 'helpRequests',
    component: () => import('@/pages/help-requests/index.vue'),
    meta: { title: '求助管理', requiresAuth: true },
  },
  {
    path: '/help-requests/:id',
    name: 'helpRequestDetail',
    component: () => import('@/pages/help-requests/detail.vue'),
    meta: { title: '求助详情', requiresAuth: true },
  },
  {
    path: '/users',
    name: 'users',
    component: () => import('@/pages/users/index.vue'),
    meta: { title: '用户管理', requiresAuth: true },
  },
  {
    path: '/users/:openId',
    name: 'userDetail',
    component: () => import('@/pages/users/detail.vue'),
    meta: { title: '用户详情', requiresAuth: true },
  },
  {
    path: '/admins',
    name: 'admins',
    component: () => import('@/pages/admins/index.vue'),
    meta: { title: '管理员管理', requiresAuth: true, requiresSuperAdmin: true },
  },
  {
    path: '/admins/create',
    name: 'adminCreate',
    component: () => import('@/pages/admins/create.vue'),
    meta: { title: '创建管理员', requiresAuth: true, requiresSuperAdmin: true },
  },
  {
    path: '/admins/:id',
    name: 'adminDetail',
    component: () => import('@/pages/admins/detail.vue'),
    meta: { title: '管理员详情', requiresAuth: true, requiresSuperAdmin: true },
  },
  {
    path: '/audit-logs',
    name: 'auditLogs',
    component: () => import('@/pages/audit-logs/index.vue'),
    meta: { title: '操作日志', requiresAuth: true, requiresSuperAdmin: true },
  },
  {
    path: '/',
    redirect: '/dashboard',
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/dashboard',
  },
]

const router = createRouter({
  history: createWebHashHistory('/admin/'),
  routes,
})

router.beforeEach((to) => {
  const store = useAdminStore()

  if (to.name === 'login' && store.isAuthenticated) {
    return { name: 'dashboard' }
  }

  if (to.meta.requiresAuth && !store.isAuthenticated) {
    return { name: 'login' }
  }

  if (to.meta.requiresSuperAdmin && !store.isSuperAdmin) {
    return { name: 'dashboard' }
  }

  return true
})

export default router
