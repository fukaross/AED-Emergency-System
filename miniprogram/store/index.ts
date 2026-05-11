/**
 * Pinia Store 入口
 *
 * 所有 Store 在 main.ts 中通过 createPinia() 注册。
 * 本文件统一导出各业务域 Store，方便页面引用。
 */
export { useAedStore } from './aed'
export { useUserStore } from './user'
export { useAuthStore } from './auth'
export { useHelpStore } from './help'
export { useCourseStore } from './course'
export { useSettingsStore } from './settings'
export { useVitalsStore } from './vitals'
