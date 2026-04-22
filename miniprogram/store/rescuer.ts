/**
 * 急救员中心状态 Store
 *
 * 职责：管理急救员列表、详情弹窗、加载状态
 * 不管理登录态（由 auth store 管理），不管理位置（由页面传入）
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { fetchNearbyRescuers, fetchRescuerDetail } from '@/api/rescuer'
import type { RescuerDetail, RescuerListResponse } from '@/types/rescuer'

export const useRescuerStore = defineStore('rescuer', () => {
  // ===== State =====
  const loading = ref(false)
  const loadError = ref<string | null>(null)
  const listResponse = ref<RescuerListResponse | null>(null)
  const detailLoading = ref(false)
  const currentDetail = ref<RescuerDetail | null>(null)
  const showDetail = ref(false)

  // ===== Actions =====
  async function loadNearbyRescuers(lat: number, lng: number): Promise<void> {
    loading.value = true
    loadError.value = null
    try {
      const res = await fetchNearbyRescuers({ lat, lng })
      listResponse.value = res
    } catch (err) {
      loadError.value = '加载急救员失败，请稍后重试'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function loadRescuerDetail(id: string): Promise<RescuerDetail> {
    detailLoading.value = true
    try {
      return await fetchRescuerDetail(id)
    } finally {
      detailLoading.value = false
    }
  }

  async function openRescuerDetail(id: string): Promise<void> {
    try {
      const detail = await loadRescuerDetail(id)
      currentDetail.value = detail
      showDetail.value = true
    } catch (err) {
      showDetail.value = false
      clearDetail()
      uni.showToast({ title: '加载详情失败', icon: 'none' })
      throw err
    }
  }

  function closeDetail(): void {
    showDetail.value = false
  }

  function clearDetail(): void {
    currentDetail.value = null
  }

  function clearList(): void {
    listResponse.value = null
    loadError.value = null
  }

  return {
    loading,
    loadError,
    listResponse,
    detailLoading,
    currentDetail,
    showDetail,
    loadNearbyRescuers,
    loadRescuerDetail,
    openRescuerDetail,
    closeDetail,
    clearDetail,
    clearList,
  }
})
