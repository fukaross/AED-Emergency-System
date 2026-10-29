/// <reference types="vite/client" />

declare module '*.vue' {
  import { DefineComponent } from 'vue'
  // eslint-disable-next-line @typescript-eslint/no-explicit-any, @typescript-eslint/ban-types
  const component: DefineComponent<{}, {}, any>
  export default component
}

interface ImportMetaEnv {
  /** 运行档位：mock | local-demo | wechat */
  readonly VITE_APP_MODE: string
  /** 后端 API 基地址（不含 /api 路径段）；mock 档可为空 */
  readonly VITE_API_BASE_URL: string
  /** 腾讯地图 Key（地址解析用，可为空） */
  readonly VITE_TENCENT_MAP_KEY: string
  /** 构建环境名：development | preview | production */
  readonly VITE_APP_ENV: string
  /** 是否恢复本地登录态；production 下默认 true */
  readonly VITE_RESTORE_AUTH_SESSION: string
  /** 单张图片上传上限（字节） */
  readonly VITE_UPLOAD_MAX_SIZE: string
  /** 求助告警订阅消息模板 ID；为空时订阅能力显示不可用 */
  readonly VITE_WECHAT_ALERT_TEMPLATE_ID: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
