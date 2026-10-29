// 错误码、上传等全局常量 —— 页面里别硬编码数字。
// 站点相关内容（机构名、标题、校区、地图中心、检索城市、服务半径、热线）
// 一律来自 config/site.json，见 config/site-config.ts，本文件不再保存站点常量。

// 错误码
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
  DUPLICATE_HELP_REQUEST: 1009,
  REPORT_PROCESSING: 1010,
  REPORT_CONTACT_REQUIRED: 1011,
  SUBSCRIBE_NOT_GRANTED: 1012,
  HELP_ALREADY_CLOSED: 1014,
  HELP_REPORT_MISSING_FIELDS: 1015,
  RESCUE_SESSION_CLOSED: 1016,
  RESCUE_NOT_PARTICIPANT: 1017,
  ACCOUNT_DISABLED: 1018,
  AED_NOT_FOUND: 2001,
  COURSE_NOT_FOUND: 2002,
  ARCHIVE_NOT_FOUND: 2003,
  FILE_TOO_LARGE: 3001,
  INVALID_FILE_TYPE: 3002,
  RATE_LIMITED: 3003,
  INTERNAL_ERROR: 5000,
} as const

/** 错误码对应的用户提示 */
export const ERROR_MESSAGES: Record<number, string> = {
  [ERROR_CODES.INVALID_PARAMS]: '请求参数有误，请检查后重试',
  [ERROR_CODES.UNAUTHORIZED]: '登录已过期，请重新登录',
  [ERROR_CODES.FORBIDDEN]: '没有权限执行此操作',
  [ERROR_CODES.RESOURCE_NOT_FOUND]: '请求的资源不存在',
  [ERROR_CODES.RESOURCE_OFFLINE]: '该资源已下线',
  [ERROR_CODES.RATE_LIMITED_HINT]: '操作过于频繁，请稍后再试',
  [ERROR_CODES.LOCATION_REQUIRED]: '需要定位信息，请开启位置权限',
  [ERROR_CODES.UPLOAD_FAILED]: '文件上传失败，请重试',
  [ERROR_CODES.DUPLICATE_HELP_REQUEST]: '已有进行中的求助',
  [ERROR_CODES.HELP_ALREADY_CLOSED]: '该求助已结束',
  [ERROR_CODES.HELP_REPORT_MISSING_FIELDS]: '请补充救助报告信息',
  [ERROR_CODES.RESCUE_SESSION_CLOSED]: '求助已结束，急救会话已关闭',
  [ERROR_CODES.RESCUE_NOT_PARTICIPANT]: '仅会话参与者可执行此操作',
  [ERROR_CODES.ACCOUNT_DISABLED]: '账号已禁用',
  [ERROR_CODES.REPORT_PROCESSING]: '报修正在处理中',
  [ERROR_CODES.REPORT_CONTACT_REQUIRED]: '请填写联系方式',
  [ERROR_CODES.SUBSCRIBE_NOT_GRANTED]: '订阅消息未授权',
  [ERROR_CODES.AED_NOT_FOUND]: 'AED 设备不存在',
  [ERROR_CODES.COURSE_NOT_FOUND]: '课程不存在',
  [ERROR_CODES.ARCHIVE_NOT_FOUND]: '档案不存在',
  [ERROR_CODES.RATE_LIMITED]: '请求过快，请稍后再试',
  [ERROR_CODES.FILE_TOO_LARGE]: '文件超过大小限制',
  [ERROR_CODES.INVALID_FILE_TYPE]: '不支持的文件格式',
  [ERROR_CODES.INTERNAL_ERROR]: '服务器内部错误，请稍后重试',
}

// 地图缩放
export const DEFAULT_MAP_ZOOM = 16
export const MIN_MAP_ZOOM = 12
export const MAX_MAP_ZOOM = 20

// 急救员单次手动离线最长时间（小时）
export const RESCUER_MAX_MANUAL_OFFLINE_HOURS = 10

export const RESCUER_MAX_MANUAL_OFFLINE_MS =
  RESCUER_MAX_MANUAL_OFFLINE_HOURS * 60 * 60 * 1000

// 网络
export const REQUEST_TIMEOUT = 10000
export const TOKEN_STORAGE_KEY = 'aed_auth_token'
export const TOKEN_EXPIRE_KEY = 'aed_auth_expire'
export const CURRENT_USER_ID_STORAGE_KEY = 'aed_current_user_id'
export const AUTH_USER_INFO_STORAGE_KEY = 'aed_auth_user_info'

// 上传
export const MAX_UPLOAD_SIZE = Number(
  import.meta.env.VITE_UPLOAD_MAX_SIZE || 5242880
)
export const ALLOWED_IMAGE_TYPES = ['jpg', 'jpeg', 'png', 'webp'] as const
export const MAX_HELP_IMAGES = 3
export const MAX_REPORT_IMAGES = 3

// 急救报警订阅消息模板 ID
export const WECHAT_ALERT_TEMPLATE_ID =
  import.meta.env.VITE_WECHAT_ALERT_TEMPLATE_ID || ''

// 缓存
export const AED_CACHE_KEY = 'aed_nearby_cache'
export const AED_CACHE_TTL = 5 * 60 * 1000

// 急救员级别的中文显示名
export const LEVEL_LABELS = {
  honor: '荣誉急救员',
  trainee: '预备急救员',
  official: '正式急救员',
  core: '骨干急救员',
} as const
