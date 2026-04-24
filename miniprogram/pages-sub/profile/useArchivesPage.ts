import { ref, watch } from 'vue'
import { onReachBottom, onShow } from '@dcloudio/uni-app'
import { auditCallResponder, auditViewArchivePhone } from '@/api/audit'
import { fetchArchivePhone, fetchArchives } from '@/api/user'
import { useAuthStore } from '@/store/auth'
import type { ArchiveItem, ArchiveListResponse } from '@/types/user'

interface FilterTag {
  readonly label: string
  readonly value: string | null
}

export const FILTER_TAGS: readonly FilterTag[] = [
  { label: '全部', value: null },
  { label: '荣誉', value: 'honor' },
  { label: '骨干', value: 'core' },
  { label: '正式', value: 'official' },
  { label: '预备', value: 'trainee' },
] as const

const PAGE_SIZE = 20
const SEARCH_DELAY_MS = 300

export function useArchivesPage() {
  const authStore = useAuthStore()
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref<string | null>(null)
  const keyword = ref('')
  const activeLevel = ref<string | null>(null)
  const page = ref(1)
  const hasMore = ref(false)
  const canViewFullPhone = ref(false)
  const selfArchive = ref<ArchiveItem | null>(null)
  const items = ref<ArchiveItem[]>([])
  const showPhoneModal = ref(false)
  const selectedArchive = ref<ArchiveItem | null>(null)
  const purpose = ref('')
  const fullPhone = ref<string | null>(null)
  const phoneLoading = ref(false)
  let searchTimer: ReturnType<typeof setTimeout> | null = null

  async function loadArchives(append = false): Promise<void> {
    startLoading(append)

    try {
      const res = await fetchArchives(buildArchiveParams(append))
      applyArchiveResponse(res, append)
    } catch {
      handleArchiveLoadError(append)
    } finally {
      loading.value = false
      loadingMore.value = false
    }
  }

  function buildArchiveParams(append: boolean): Record<string, unknown> {
    const params: Record<string, unknown> = {
      page: append ? page.value : 1,
      pageSize: PAGE_SIZE,
    }
    if (keyword.value.trim()) params.keyword = keyword.value.trim()
    if (activeLevel.value) params.level = activeLevel.value
    return params
  }

  function applyArchiveResponse(res: ArchiveListResponse, append: boolean) {
    canViewFullPhone.value = res.canViewFullPhone
    selfArchive.value = res.selfArchive ?? null
    items.value = append ? [...items.value, ...res.items] : [...res.items]
    page.value = append ? page.value + 1 : 2
    hasMore.value = items.value.length < res.total
  }

  function startLoading(append: boolean) {
    if (append) {
      loadingMore.value = true
      return
    }

    error.value = null
    loading.value = true
  }

  function handleArchiveLoadError(append: boolean) {
    if (append) {
      uni.showToast({ title: '加载失败', icon: 'none' })
      return
    }

    error.value = authStore.isLoggedIn ? '加载失败' : '请先登录'
  }

  function handleSearch() {
    if (searchTimer) clearTimeout(searchTimer)
    searchTimer = setTimeout(() => {
      page.value = 1
      loadArchives(false)
    }, SEARCH_DELAY_MS)
  }

  function handleSearchConfirm() {
    if (searchTimer) clearTimeout(searchTimer)
    page.value = 1
    loadArchives(false)
  }

  function handleLevelClick(level: string | null) {
    if (activeLevel.value === level) return
    activeLevel.value = level
    page.value = 1
    loadArchives(false)
  }

  function handleRetry() {
    page.value = 1
    loadArchives(false)
  }

  function handleCardClick(archive: ArchiveItem) {
    if (!canViewFullPhone.value) return
    selectedArchive.value = archive
    purpose.value = ''
    fullPhone.value = null
    showPhoneModal.value = true
  }

  function closePhoneModal() {
    showPhoneModal.value = false
    selectedArchive.value = null
    purpose.value = ''
    fullPhone.value = null
  }

  async function confirmViewPhone() {
    if (!purpose.value.trim() || phoneLoading.value || !selectedArchive.value) return

    phoneLoading.value = true
    try {
      const phone = await fetchArchivePhone(selectedArchive.value.id, purpose.value.trim())
      fullPhone.value = phone.phone
      auditViewArchivePhone(selectedArchive.value.id, purpose.value.trim())
    } catch {
      uni.showToast({ title: '获取联系方式失败', icon: 'none' })
    } finally {
      phoneLoading.value = false
    }
  }

  function handleCall() {
    if (phoneLoading.value || !fullPhone.value || !selectedArchive.value) return
    auditCallResponder(selectedArchive.value.id)
    uni.makePhoneCall({
      phoneNumber: fullPhone.value,
      fail: () => uni.showToast({ title: '拨号失败', icon: 'none' }),
    })
  }

  watch(keyword, handleSearch)

  onShow(() => {
    if (!authStore.isLoggedIn) {
      error.value = '请先登录'
      return
    }

    page.value = 1
    loadArchives(false)
  })

  onReachBottom(() => {
    if (loadingMore.value || !hasMore.value || loading.value) return
    loadArchives(true)
  })

  return {
    FILTER_TAGS,
    activeLevel,
    canViewFullPhone,
    closePhoneModal,
    confirmViewPhone,
    error,
    fullPhone,
    handleCall,
    handleCardClick,
    handleLevelClick,
    handleRetry,
    handleSearchConfirm,
    items,
    keyword,
    loading,
    loadingMore,
    phoneLoading,
    purpose,
    selectedArchive,
    selfArchive,
    showPhoneModal,
  }
}
