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
    path: '/rescue-sessions',
    name: 'rescueSessions',
    component: () => import('@/pages/rescue-sessions/index.vue'),
    meta: { title: '急救会话', requiresAuth: true },
  },
  {
    path: '/rescue-sessions/:helpId',
    name: 'rescueSessionDetail',
    component: () => import('@/pages/rescue-sessions/detail.vue'),
    meta: { title: '会话监控', requiresAuth: true },
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
    path: '/devices',
    name: 'devices',
    component: () => import('@/pages/devices/index.vue'),
    meta: { title: 'AED 管理', requiresAuth: true },
  },
  {
    path: '/devices/:id',
    name: 'deviceDetail',
    component: () => import('@/pages/devices/detail.vue'),
    meta: { title: 'AED 详情', requiresAuth: true },
  },
  {
    path: '/rescuers',
    name: 'rescuers',
    component: () => import('@/pages/rescuers/index.vue'),
    meta: { title: '急救员管理', requiresAuth: true },
  },
  {
    path: '/rescuers/:id',
    name: 'rescuerDetail',
    component: () => import('@/pages/rescuers/detail.vue'),
    meta: { title: '急救员详情', requiresAuth: true },
  },
  {
    path: '/volunteer-applications',
    name: 'volunteerApplications',
    component: () => import('@/pages/volunteer-applications/index.vue'),
    meta: { title: '志愿者审核', requiresAuth: true },
  },
  {
    path: '/volunteer-applications/:id',
    name: 'volunteerApplicationDetail',
    component: () => import('@/pages/volunteer-applications/detail.vue'),
    meta: { title: '申请详情', requiresAuth: true },
  },
  {
    path: '/school-profiles',
    name: 'schoolProfiles',
    component: () => import('@/pages/school-profiles/index.vue'),
    meta: { title: '学校档案', requiresAuth: true },
  },
  {
    path: '/school-profiles/:id',
    name: 'schoolProfileDetail',
    component: () => import('@/pages/school-profiles/detail.vue'),
    meta: { title: '学校档案详情', requiresAuth: true },
  },
  {
    path: '/school-bindings',
    name: 'schoolBindings',
    component: () => import('@/pages/school-bindings/index.vue'),
    meta: { title: '学校绑定审核', requiresAuth: true },
  },
  {
    path: '/school-bindings/:id',
    name: 'schoolBindingDetail',
    component: () => import('@/pages/school-bindings/detail.vue'),
    meta: { title: '绑定审核详情', requiresAuth: true },
  },
  {
    path: '/responder-claims',
    name: 'responderClaims',
    component: () => import('@/pages/responder-claims/index.vue'),
    meta: { title: '急救员认领审核', requiresAuth: true },
  },
  {
    path: '/responder-claims/:id',
    name: 'responderClaimDetail',
    component: () => import('@/pages/responder-claims/detail.vue'),
    meta: { title: '认领审核详情', requiresAuth: true },
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
    path: '/classroom',
    redirect: '/classroom/summary',
    meta: { requiresAuth: true },
  },
  {
    path: '/classroom/summary',
    name: 'classroomSummary',
    component: () => import('@/pages/classroom/summary/index.vue'),
    meta: { title: '统计横幅', requiresAuth: true },
  },
  {
    path: '/classroom/quick-actions',
    name: 'classroomQuickActions',
    component: () => import('@/pages/classroom/quick-actions/index.vue'),
    meta: { title: '关键步骤', requiresAuth: true },
  },
  {
    path: '/classroom/quick-actions/:id',
    name: 'classroomQuickActionDetail',
    component: () => import('@/pages/classroom/quick-actions/detail.vue'),
    meta: { title: '关键步骤详情', requiresAuth: true },
  },
  {
    path: '/classroom/courses',
    name: 'classroomCourses',
    component: () => import('@/pages/classroom/courses/index.vue'),
    meta: { title: '课程管理', requiresAuth: true },
  },
  {
    path: '/classroom/courses/:id',
    name: 'classroomCourseDetail',
    component: () => import('@/pages/classroom/courses/detail.vue'),
    meta: { title: '课程详情', requiresAuth: true },
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
