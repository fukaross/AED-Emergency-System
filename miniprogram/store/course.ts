/**
 * 课程 Store
 *
 * 管理轮播横幅、课程列表、当前课程详情
 * Mock 与真实接口分流统一在 API 层处理，Store 不做静默回退
 *
 * 来源：CLAUDE.md §6.3
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { BannerItem } from '@/types/common'
import type { CourseSummary, CourseDetail } from '@/types/user'
import { fetchBanners } from '@/api/common'
import { fetchCourseList, fetchCourseDetail } from '@/api/course'

export const useCourseStore = defineStore('course', () => {
  // ===== State =====
  const banners = ref<BannerItem[]>([])
  const courseList = ref<CourseSummary[]>([])
  const currentCourse = ref<CourseDetail | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  // ===== Actions =====
  async function loadBanners(): Promise<void> {
    error.value = null
    try {
      banners.value = await fetchBanners()
    } catch (err) {
      error.value = (err as Error).message
      banners.value = []
    }
  }

  async function loadCourseList(category?: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await fetchCourseList(
        category ? { category } : undefined,
      )
      courseList.value = [...result.items]
    } catch (err) {
      error.value = (err as Error).message
      courseList.value = []
    } finally {
      loading.value = false
    }
  }

  async function loadCourseDetail(id: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      currentCourse.value = await fetchCourseDetail(id)
    } catch (err) {
      error.value = (err as Error).message
      currentCourse.value = null
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
    error,
    loadBanners,
    loadCourseList,
    loadCourseDetail,
    clearCurrentCourse,
  }
})
