/**
 * 课程 Store
 *
 * 管理轮播横幅、课程列表、当前课程详情
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

  // ===== Actions =====
  async function loadBanners(): Promise<void> {
    banners.value = await fetchBanners()
  }

  async function loadCourseList(category?: string): Promise<void> {
    loading.value = true
    try {
      const result = await fetchCourseList(
        category ? { category } : undefined,
      )
      courseList.value = [...result.items]
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
