/**
 * 求助页面与匿名会话流程 composable
 *
 * 编排求助发布流程：定位 → 填写 → 上传图片 → 发布
 * 支持游客匿名会话（返回 requestNo/followupCode/closeToken）
 *
 * D-05 基线要求
 */
import { ref, reactive } from 'vue'
import { useHelpStore } from '@/store/help'
import { useAuthStore } from '@/store/auth'
import { getCurrentLocation } from '@/services/geo-service'
import { uploadImage } from '@/api/common'
import type { PublishHelpPayload, PublishHelpResponse } from '@/types/help'
import { MAX_HELP_IMAGES, SCHOOL_EMERGENCY_PHONE } from '@/utils/constants'

export type HelpType = 'help_hand' | 'call_120' | 'call_school' | 'general'

export function useEmergencyHelp() {
  const helpStore = useHelpStore()
  const authStore = useAuthStore()

  const form = reactive({
    address: '',
    content: '',
    lat: 0,
    lng: 0,
    images: [] as string[],
    type: 'general' as HelpType,
    privacyConsent: false,
  })

  const isSubmitting = ref(false)
  const publishResult = ref<PublishHelpResponse | null>(null)

  /** 自动定位并填入地址 */
  async function autoLocate(): Promise<void> {
    const loc = await getCurrentLocation({ mode: 'interactive' })

    if (loc.success) {
      form.lat = loc.point.lat
      form.lng = loc.point.lng
      form.address =
        loc.address ||
        loc.name ||
        form.address ||
        '定位成功，请补充具体楼栋/楼层信息'
      return
    }

    uni.showToast({
      title: loc.errorMessage || '定位失败，请手动输入地址',
      icon: 'none',
    })
  }

  /** 快捷操作：找人手 */
  function quickHelpHand(): void {
    form.type = 'help_hand'
    form.content = '需要急救人手支援'
  }

  /** 快捷操作：找 120 */
  function quickCall120(): void {
    uni.makePhoneCall({ phoneNumber: '120' })
  }

  /** 快捷操作：找校方 */
  function quickCallSchool(): void {
    uni.makePhoneCall({ phoneNumber: SCHOOL_EMERGENCY_PHONE })
  }

  /** 选择并上传图片 */
  async function addImage(): Promise<void> {
    if (form.images.length >= MAX_HELP_IMAGES) {
      uni.showToast({ title: `最多上传${MAX_HELP_IMAGES}张图片`, icon: 'none' })
      return
    }

    const res = await uni.chooseImage({
      count: MAX_HELP_IMAGES - form.images.length,
      sizeType: ['compressed'],
    })

    for (const path of res.tempFilePaths) {
      const uploaded = await uploadImage(path, 'help')
      form.images.push(uploaded.url)
    }
  }

  /** 发布求助 */
  async function submit(): Promise<PublishHelpResponse | null> {
    if (!form.address.trim()) {
      uni.showToast({ title: '请填写详细位置', icon: 'none' })
      return null
    }
    if (!form.content.trim()) {
      uni.showToast({ title: '请填写帮助内容', icon: 'none' })
      return null
    }
    if (!form.privacyConsent) {
      uni.showToast({ title: '请同意隐私说明', icon: 'none' })
      return null
    }

    isSubmitting.value = true
    try {
      const payload: PublishHelpPayload = {
        lat: form.lat,
        lng: form.lng,
        address: form.address,
        content: form.content,
        type: form.type,
        images: form.images.length > 0 ? form.images : undefined,
        requestChannel: authStore.isLoggedIn ? 'user' : 'guest',
        privacyConsentAt: new Date().toISOString(),
      }

      const result = await helpStore.publish(payload)
      publishResult.value = result
      return result
    } finally {
      isSubmitting.value = false
    }
  }

  return {
    form,
    isSubmitting,
    publishResult,
    autoLocate,
    quickHelpHand,
    quickCall120,
    quickCallSchool,
    addImage,
    submit,
  }
}
