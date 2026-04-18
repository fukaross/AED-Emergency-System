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
  readonly VITE_USE_REAL_LOCATION: string
  readonly VITE_AUTO_LOCATE_ON_INIT: string
  readonly VITE_SCHOOL_EMERGENCY_PHONE: string
  readonly VITE_UPLOAD_MAX_SIZE: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
