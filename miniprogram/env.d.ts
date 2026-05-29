/// <reference types="vite/client" />

declare module '*.vue' {
  import { DefineComponent } from 'vue'
  // eslint-disable-next-line @typescript-eslint/no-explicit-any, @typescript-eslint/ban-types
  const component: DefineComponent<{}, {}, any>
  export default component
}

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
  readonly VITE_TENCENT_MAP_KEY: string
  readonly VITE_APP_ENV: string
  readonly VITE_USE_MOCK: string
  readonly VITE_USE_PUBLIC_DEMO_DATA: string
  readonly VITE_USE_REAL_WECHAT_FLOW: string
  readonly VITE_RESCUER_BACKEND_READY: string
  readonly VITE_RESTORE_AUTH_SESSION: string
  readonly VITE_USE_REAL_LOCATION: string
  readonly VITE_AUTO_LOCATE_ON_INIT: string
  readonly VITE_SCHOOL_EMERGENCY_PHONE: string
  readonly VITE_UPLOAD_MAX_SIZE: string
  readonly VITE_WECHAT_ALERT_TEMPLATE_ID: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
