import { computed, onMounted, reactive, ref } from 'vue'
import { uploadImage } from '@/api/common'
import { useAuthStore } from '@/store/auth'
import { useUserStore } from '@/store/user'
import type {
  BindingStatus,
  Gender,
  RescuerLevel,
  UpdateUserProfilePayload,
} from '@/types/user'

const DEFAULT_AVATAR = '/static/images/avatar-default.png'
export const CAMERA_ICON = '/static/icons/camera.svg'
const BYTES_PER_MB = 1024 * 1024
const AVATAR_MAX_BYTES = 2 * BYTES_PER_MB
const COMPRESS_QUALITY = 80
const PHONE_PATTERN = /^1[3-9]\d{9}$/

export const GENDER_OPTIONS: readonly { value: Gender; label: string }[] = [
  { value: 'male', label: '男' },
  { value: 'female', label: '女' },
  { value: 'unknown', label: '保密' },
]

const BINDING_LABELS: Record<BindingStatus, string> = {
  active: '已绑定',
  pending: '审核中',
  rejected: '已拒绝',
  replaced: '已替换',
  unbound: '未绑定',
}

interface EditableProfile {
  name: string
  gender: Gender
  phone: string
}

interface UpdateUserProfileDraft {
  name?: string
  avatarUrl?: string
  gender?: Gender
  phone?: string
}

function getSafeAreaBottomPx(): string {
  try {
    const info = uni.getSystemInfoSync()
    return `calc(24rpx + ${info.safeAreaInsets?.bottom ?? 0}px)`
  } catch {
    return '24rpx'
  }
}

function chooseAvatarImage(): Promise<UniApp.ChooseImageSuccessCallbackResult | null> {
  return new Promise<UniApp.ChooseImageSuccessCallbackResult>((resolve, reject) => {
    uni.chooseImage({
      count: 1,
      sizeType: ['compressed'],
      success: resolve,
      fail: reject,
    })
  }).catch(() => null)
}

function getChosenFileSize(
  res: UniApp.ChooseImageSuccessCallbackResult,
): number {
  const files: { size?: number }[] = Array.isArray(res.tempFiles)
    ? res.tempFiles
    : []
  return files[0]?.size ?? 0
}

function compressAvatarImage(path: string): Promise<{ tempFilePath: string }> {
  return new Promise((resolve, reject) => {
    uni.compressImage({
      src: path,
      quality: COMPRESS_QUALITY,
      success: (res) => resolve(res),
      fail: reject,
    })
  })
}

export function useProfileEditForm() {
  const authStore = useAuthStore()
  const userStore = useUserStore()
  const original = reactive<EditableProfile>({
    name: '',
    gender: 'unknown',
    phone: '',
  })
  const form = reactive<EditableProfile>({
    name: '',
    gender: 'unknown',
    phone: '',
  })
  const avatarUrl = ref('')
  const avatarLocalPath = ref('')
  const phoneDirty = ref(false)
  const saving = ref(false)
  const displayAvatar = computed(
    () => avatarLocalPath.value || avatarUrl.value || DEFAULT_AVATAR,
  )
  const schoolProfile = computed(() => userStore.userInfo?.schoolProfile ?? null)
  const rescuerLevel = computed<RescuerLevel | null>(
    () => userStore.userInfo?.level ?? null,
  )
  const bindingStatusLabel = computed(() => {
    const status = schoolProfile.value?.bindingStatus
    return status ? BINDING_LABELS[status] : ''
  })
  const isDirty = computed(
    () =>
      form.name.trim() !== original.name.trim() ||
      form.gender !== original.gender ||
      form.phone !== original.phone ||
      avatarLocalPath.value !== '',
  )
  const safeAreaBottomPx = getSafeAreaBottomPx()

  function initForm(): void {
    const profile = userStore.userInfo
    original.name = profile?.name ?? ''
    original.gender = profile?.gender ?? 'unknown'
    original.phone = profile?.phone ?? ''
    form.name = original.name
    form.gender = original.gender
    form.phone = original.phone
    avatarUrl.value = profile?.avatarUrl || ''
    avatarLocalPath.value = ''
    phoneDirty.value = false
  }

  async function init(): Promise<void> {
    await authStore.checkAuth()
    if (!authStore.isLoggedIn) {
      uni.showToast({ title: '请先登录', icon: 'none' })
      uni.navigateBack()
      return
    }
    if (!userStore.userInfo) {
      await userStore.loadProfile()
    }
    initForm()
  }

  function onPhoneInput(): void {
    phoneDirty.value = true
  }

  async function handleChooseAvatar(): Promise<void> {
    const res = await chooseAvatarImage()
    if (!res?.tempFilePaths?.[0]) return
    const path = res.tempFilePaths[0]
    if (getChosenFileSize(res) <= AVATAR_MAX_BYTES) {
      avatarLocalPath.value = path
      return
    }
    try {
      avatarLocalPath.value = (await compressAvatarImage(path)).tempFilePath
    } catch {
      uni.showToast({ title: '图片过大，请选择小于2MB的图片', icon: 'none' })
    }
  }

  function validate(): string | null {
    if (!form.name.trim()) return '请输入昵称'
    if (form.phone && !PHONE_PATTERN.test(form.phone)) {
      return '请输入正确的手机号'
    }
    return null
  }

  async function buildPayload(): Promise<UpdateUserProfilePayload> {
    let finalAvatarUrl = avatarUrl.value
    if (avatarLocalPath.value) {
      finalAvatarUrl = (await uploadImage(avatarLocalPath.value, 'avatar')).url
    }
    const payload: UpdateUserProfileDraft = {}
    if (form.name.trim() !== original.name) payload.name = form.name.trim()
    if (form.gender !== original.gender) payload.gender = form.gender
    if (phoneDirty.value) payload.phone = form.phone
    if (finalAvatarUrl !== avatarUrl.value) payload.avatarUrl = finalAvatarUrl
    return payload
  }

  async function handleSave(): Promise<void> {
    if (saving.value) return
    const error = validate()
    if (error) {
      uni.showToast({ title: error, icon: 'none' })
      return
    }
    saving.value = true
    try {
      const payload = await buildPayload()
      if (Object.keys(payload).length > 0) await userStore.saveProfile(payload)
      uni.showToast({ title: '已保存', icon: 'success' })
      uni.navigateBack()
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : '保存失败，请重试'
      uni.showToast({ title: msg, icon: 'none' })
    } finally {
      saving.value = false
    }
  }

  function handleBeforeBack(): Promise<boolean> {
    if (!isDirty.value) return Promise.resolve(true)
    return new Promise((resolve) => {
      uni.showModal({
        title: '提示',
        content: '没有保存修改！是否保存？',
        confirmText: '保存',
        cancelText: '不保存',
        success: (res) => {
          if (res.confirm) {
            void handleSave()
            resolve(false)
            return
          }
          resolve(true)
        },
        fail: () => {
          uni.showToast({ title: '请先保存', icon: 'none' })
          resolve(false)
        },
      })
    })
  }

  onMounted(init)

  return {
    CAMERA_ICON,
    GENDER_OPTIONS,
    bindingStatusLabel,
    displayAvatar,
    form,
    handleBeforeBack,
    handleChooseAvatar,
    handleSave,
    onPhoneInput,
    rescuerLevel,
    safeAreaBottomPx,
    saving,
    schoolProfile,
  }
}
