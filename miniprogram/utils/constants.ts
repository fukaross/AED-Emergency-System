/**
 * 常量定义
 *
 * 错误码、API 路径、业务常量统一管理
 * 来源：BackEnd.md §4.1 错误码定义 + CLAUDE.md §8.2
 *
 * ⚠️ 禁止在页面/组件中硬编码魔法数字，统一引用本文件常量。
 */

// ===== 错误码 =====
export const ERROR_CODES = {
  SUCCESS: 0,
  INVALID_PARAMS: 1001,
  UNAUTHORIZED: 1002,
  FORBIDDEN: 1003,
  RESOURCE_NOT_FOUND: 1004,
  RESOURCE_OFFLINE: 1005,
  RATE_LIMITED_HINT: 1006,
  LOCATION_REQUIRED: 1007,
  UPLOAD_FAILED: 1008,
  HELP_ALREADY_CLOSED: 1009,
  REPORT_PROCESSING: 1010,
  REPORT_CONTACT_REQUIRED: 1011,
  AED_NOT_FOUND: 2001,
  COURSE_NOT_FOUND: 2002,
  ARCHIVE_NOT_FOUND: 2003,
  RATE_LIMITED: 3001,
  FILE_TOO_LARGE: 3002,
  INVALID_FILE_TYPE: 3003,
  INTERNAL_ERROR: 5000,
} as const

/** 错误码对应的用户友好提示 */
export const ERROR_MESSAGES: Record<number, string> = {
  [ERROR_CODES.INVALID_PARAMS]: '请求参数有误，请检查后重试',
  [ERROR_CODES.UNAUTHORIZED]: '登录已过期，请重新登录',
  [ERROR_CODES.FORBIDDEN]: '没有权限执行此操作',
  [ERROR_CODES.RESOURCE_NOT_FOUND]: '请求的资源不存在',
  [ERROR_CODES.RESOURCE_OFFLINE]: '该资源已下线',
  [ERROR_CODES.RATE_LIMITED_HINT]: '操作过于频繁，请稍后再试',
  [ERROR_CODES.LOCATION_REQUIRED]: '需要定位信息，请开启位置权限',
  [ERROR_CODES.UPLOAD_FAILED]: '文件上传失败，请重试',
  [ERROR_CODES.HELP_ALREADY_CLOSED]: '该求助已结束',
  [ERROR_CODES.REPORT_PROCESSING]: '报修正在处理中',
  [ERROR_CODES.REPORT_CONTACT_REQUIRED]: '请填写联系方式',
  [ERROR_CODES.AED_NOT_FOUND]: 'AED 设备不存在',
  [ERROR_CODES.COURSE_NOT_FOUND]: '课程不存在',
  [ERROR_CODES.ARCHIVE_NOT_FOUND]: '档案不存在',
  [ERROR_CODES.RATE_LIMITED]: '请求过快，请稍后再试',
  [ERROR_CODES.FILE_TOO_LARGE]: '文件超过大小限制',
  [ERROR_CODES.INVALID_FILE_TYPE]: '不支持的文件格式',
  [ERROR_CODES.INTERNAL_ERROR]: '服务器内部错误，请稍后重试',
}

// ===== 地图常量 =====
export const MAP_CENTER = {
  latitude: 19.974394,
  longitude: 110.517669,
} as const

export const DEFAULT_MAP_ZOOM = 16
export const MIN_MAP_ZOOM = 12
export const MAX_MAP_ZOOM = 20

/** 默认搜索半径 (米) */
export const DEFAULT_SEARCH_RADIUS = 3000

// ===== 网络常量 =====
export const REQUEST_TIMEOUT = 10000
export const TOKEN_STORAGE_KEY = 'aed_auth_token'
export const TOKEN_EXPIRE_KEY = 'aed_auth_expire'

// ===== 上传常量 =====
export const MAX_UPLOAD_SIZE = Number(
  import.meta.env.VITE_UPLOAD_MAX_SIZE || 5242880
)
export const ALLOWED_IMAGE_TYPES = ['jpg', 'jpeg', 'png', 'webp'] as const
export const MAX_HELP_IMAGES = 3
export const MAX_REPORT_IMAGES = 3

// ===== 业务常量 =====
export const HELP_RATE_LIMIT_SECONDS = 300

/** 校方急救热线 (占位值，运行时从环境变量读取) */
export const SCHOOL_EMERGENCY_PHONE =
  import.meta.env.VITE_SCHOOL_EMERGENCY_PHONE || 'REDACTED'

// ===== 缓存常量 =====
export const AED_CACHE_KEY = 'aed_nearby_cache'
export const AED_CACHE_TTL = 5 * 60 * 1000

/** 急救员级别显示名 */
export const LEVEL_LABELS = {
  honor: '荣誉急救员',
  trainee: '预备急救员',
  official: '正式急救员',
  core: '骨干急救员',
} as const
