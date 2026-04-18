/**
 * 课程 Store
 *
 * 管理轮播横幅、课程列表、当前课程详情
 * 后端未就绪时自动回退 mock 数据
 *
 * 来源：CLAUDE.md §6.3
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { BannerItem } from '@/types/common'
import type { CourseSummary, CourseDetail } from '@/types/user'
import { fetchBanners } from '@/api/common'
import { fetchCourseList, fetchCourseDetail } from '@/api/course'
import { BANNER_MOCK_DATA } from '@/mock/banner'
import { COURSE_MOCK_DATA } from '@/mock/course'

export const useCourseStore = defineStore('course', () => {
  // ===== State =====
  const banners = ref<BannerItem[]>([])
  const courseList = ref<CourseSummary[]>([])
  const currentCourse = ref<CourseDetail | null>(null)
  const loading = ref(false)

  // ===== Actions =====
  async function loadBanners(): Promise<void> {
    try {
      banners.value = await fetchBanners()
    } catch {
      banners.value = [...BANNER_MOCK_DATA]
    }
  }

  async function loadCourseList(category?: string): Promise<void> {
    loading.value = true
    try {
      const result = await fetchCourseList(
        category ? { category } : undefined,
      )
      courseList.value = [...result.items]
    } catch {
      courseList.value = [...COURSE_MOCK_DATA]
    } finally {
      loading.value = false
    }
  }

  async function loadCourseDetail(id: string): Promise<void> {
    loading.value = true
    try {
      currentCourse.value = await fetchCourseDetail(id)
    } finally {
      loading.value = false
    }
  }

  function clearCurrentCourse(): void {
    currentCourse.value = null
  }

  return {
    banners,
    courseList,
    currentCourse,
    loading,
    loadBanners,
    loadCourseList,
    loadCourseDetail,
    clearCurrentCourse,
  }
})
