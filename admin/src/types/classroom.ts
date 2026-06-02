export interface ClassroomStatItem {
  key: string
  title: string
  value: number
  sortOrder: number
}

export interface ClassroomSummary {
  items: ClassroomStatItem[]
  updatedAt: string
}

export interface AdminCourseItem {
  id: string
  title: string
  thumbnail: string
  type: 'article' | 'video'
  category: string
  pinned: boolean
  sortOrder: number
  isEnabled: boolean
  createdAt: string
}

export interface AdminCourseListResponse {
  items: AdminCourseItem[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface AdminCourseDetail extends AdminCourseItem {
  content: string
  videoUrl: string | null
  videos: CourseVideo[]
  disclaimer: string
  estimatedTime?: string | null
  viewCount?: number
  steps: CourseStep[]
}

export interface CourseVideo {
  order: number
  title: string
  url: string
}

export interface CourseStep {
  order: number
  title: string
  description: string
  image: string
  keyPoints: string[]
}

export interface CreateCoursePayload {
  title: string
  category: string
  thumbnail?: string
  type?: 'article' | 'video'
  content?: string
  videoUrl?: string
  videos?: CourseVideo[]
  disclaimer?: string
  pinned?: boolean
  sortOrder?: number
  isEnabled?: boolean
  estimatedTime?: string
  steps?: CourseStep[]
}

export interface UpdateCoursePayload {
  title?: string
  thumbnail?: string
  type?: 'article' | 'video'
  category?: string
  content?: string
  videoUrl?: string | null
  videos?: CourseVideo[]
  disclaimer?: string
  pinned?: boolean
  sortOrder?: number
  isEnabled?: boolean
  estimatedTime?: string
  steps?: CourseStep[]
}

export interface AdminQuickActionItem {
  id: string
  title: string
  icon: string
  sortOrder: number
  isEnabled: boolean
}

export interface AdminQuickActionDetail {
  id: string
  title: string
  icon: string
  summary: string
  sortOrder: number
  isEnabled: boolean
  steps: QuickActionStep[]
}

export interface QuickActionStep {
  order: number
  title: string
  description: string
  image: string
  keyPoints: string[]
}

export interface CreateQuickActionPayload {
  title: string
  icon: string
  summary?: string
  sortOrder?: number
  isEnabled?: boolean
  steps?: QuickActionStep[]
}

export interface UpdateQuickActionPayload {
  title?: string
  icon?: string
  summary?: string
  sortOrder?: number
  isEnabled?: boolean
  steps?: QuickActionStep[]
}
