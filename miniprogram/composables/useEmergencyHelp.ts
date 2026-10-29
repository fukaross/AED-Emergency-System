// 求助发布流程 —— 选点 → 填写 → 上传图片 → 发布（支持游客匿名会话）
import { ref, reactive, computed } from 'vue'
import { useHelpStore } from '@/store/help'
import { useAuthStore } from '@/store/auth'
import { requestAlertSubscribe } from '@/api/wechat-subscribe'
import {
  bindManualPhone as bindManualPhoneApi,
  bindWeChatPhone,
} from '@/services/auth-service'
import { chooseLocationPoint, getCurrentLocation } from '@/services/geo-service'
import { mergeLocationText } from '@/services/location-address-service'
import { uploadImage } from '@/api/common'
import type { PublishHelpPayload, PublishHelpResponse } from '@/types/help'
import { MAX_HELP_IMAGES } from '@/utils/constants'
import { RESCUE_PHONE } from '@/config/site-config'
import { dialPhone } from '@/utils/dial'
import { USE_REAL_WECHAT_FLOW } from '@/utils/runtime-env'

export type HelpType = 'help_hand' | 'call_120' | 'call_school' | 'general'

const PHONE_PATTERN = /^1[3-9]\d{9}$/
const HELP_LOCATION_PREFETCH_TIMEOUT_MS = 3000

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
  const isBindingPhone = ref(false)
  const phone = ref('')
  const manualPhone = ref('')
  const publishResult = ref<PublishHelpResponse | null>(null)
  const rescuePhoneConfigured = computed(() => RESCUE_PHONE !== '')

  /** 选择求助发生位置 */
  async function selectLocation(): Promise<void> {
    const userLoc = await getCurrentLocation({ timeoutMs: HELP_LOCATION_PREFETCH_TIMEOUT_MS })
    const loc = await chooseLocationPoint(
      userLoc.success ? userLoc.point : undefined,
    )

    if (loc.success) {
      form.lat = loc.point.lat
      form.lng = loc.point.lng
      form.address = mergeLocationText(loc.name, loc.address)

      if (form.address) {
        uni.showToast({ title: '位置已选择', icon: 'success' })
        return
      }

      uni.showToast({
        title: '请补充位置名称',
        icon: 'none',
      })
      return
    }

    uni.showToast({
      title: loc.errorMessage || '位置选择失败',
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
    dialPhone('120')
  }

  /** 快捷操作：找校方 —— 热线来自 config/site.json，未配置时不提供 */
  function quickCallSchool(): void {
    if (!rescuePhoneConfigured.value) {
      uni.showToast({ title: '未配置校方急救热线', icon: 'none' })
      return
    }
    dialPhone(RESCUE_PHONE)
  }

  /** 选择并上传图片 */
  async function addImage(): Promise<void> {
    const remaining = MAX_HELP_IMAGES - form.images.length
    if (remaining <= 0) {
      uni.showToast({ title: `最多上传${MAX_HELP_IMAGES}张图片`, icon: 'none' })
      return
    }

    try {
      const res = await uni.chooseImage({ count: remaining, sizeType: ['compressed'] })
      for (const path of res.tempFilePaths.slice(0, remaining)) {
        await uploadHelpImage(path)
      }
    } catch (error) {
      if (!isUserCancel(error)) {
        uni.showToast({ title: '选择失败', icon: 'none' })
      }
    }
  }

  async function uploadHelpImage(path: string): Promise<void> {
    try {
      const uploaded = await uploadImage(path, 'help')
      form.images.push(uploaded.url)
    } catch (error) {
      console.warn('[Help] 现场图片上传失败', error)
      uni.showToast({ title: '上传失败', icon: 'none' })
    }
  }

  /** 发布求助 */
  async function submit(): Promise<PublishHelpResponse | null> {
    if (!form.address.trim()) {
      uni.showToast({ title: '请选择位置', icon: 'none' })
      return null
    }
    if (!form.lat || !form.lng) {
      uni.showToast({ title: '请先选点', icon: 'none' })
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
    if (!(await ensureNoActiveHelp())) {
      return null
    }
    if (!(await ensurePhoneBeforePublish())) {
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

      await requestSubscribeBeforePublish()
      const result = await helpStore.publish(payload)
      publishResult.value = result
      return result
    } finally {
      isSubmitting.value = false
    }
  }

  async function ensureNoActiveHelp(): Promise<boolean> {
    await helpStore.loadList()
    if (!helpStore.activeRequest) return true

    uni.showToast({ title: '已有进行中的求助', icon: 'none' })
    return false
  }

  async function bindPhone(code?: string): Promise<boolean> {
    if (!code) {
      uni.showToast({ title: '可手动填写', icon: 'none' })
      return false
    }

    isBindingPhone.value = true
    try {
      if (!authStore.isLoggedIn) {
        await authStore.login()
      }

      const result = await bindWeChatPhone(code)
      phone.value = result.phone
      manualPhone.value = result.phone
      uni.showToast({ title: '电话已授权', icon: 'success' })
      return true
    } finally {
      isBindingPhone.value = false
    }
  }

  async function bindManualPhone(): Promise<boolean> {
    const input = manualPhone.value.trim()
    if (!PHONE_PATTERN.test(input)) {
      uni.showToast({ title: '请输入正确手机号', icon: 'none' })
      return false
    }

    isBindingPhone.value = true
    try {
      if (!authStore.isLoggedIn) {
        await authStore.login()
      }

      const result = await bindManualPhoneApi(input)
      phone.value = result.phone
      manualPhone.value = result.phone
      uni.showToast({ title: '电话已保存', icon: 'success' })
      return true
    } finally {
      isBindingPhone.value = false
    }
  }

  async function ensurePhoneBeforePublish(): Promise<boolean> {
    if (!USE_REAL_WECHAT_FLOW || phone.value) return true
    if (manualPhone.value.trim()) return bindManualPhone()

    uni.showToast({ title: '请填写电话', icon: 'none' })
    return false
  }

  async function requestSubscribeBeforePublish(): Promise<void> {
    if (!authStore.isLoggedIn) return

    if (!USE_REAL_WECHAT_FLOW) {
      // 演示档不得调用真实订阅消息；明确告知未发送，不伪造“通知已开启”
      uni.showToast({ title: '演示模式：求助通知未真实发送', icon: 'none' })
      return
    }

    try {
      uni.showToast({ title: '请点允许通知', icon: 'none' })
      const status = await requestAlertSubscribe()
      if (status === 'accept' || status === 'acceptWithAudio') {
        uni.showToast({ title: '通知已开启', icon: 'success' })
        return
      }

      uni.showToast({ title: '请开启通知', icon: 'none' })
    } catch (error) {
      console.warn('[Help] 订阅授权失败', error)
      uni.showToast({ title: '请开启通知', icon: 'none' })
    }
  }

  function previewImage(index: number): void {
    uni.previewImage({ current: form.images[index], urls: form.images })
  }

  function removeImage(index: number): void {
    form.images.splice(index, 1)
  }

  function isUserCancel(error: unknown): boolean {
    return String((error as { errMsg?: string })?.errMsg || error).includes('cancel')
  }

  return {
    form,
    isSubmitting,
    isBindingPhone,
    phone,
    manualPhone,
    publishResult,
    rescuePhoneConfigured,
    selectLocation,
    quickHelpHand,
    quickCall120,
    quickCallSchool,
    addImage,
    previewImage,
    removeImage,
    bindPhone,
    bindManualPhone,
    submit,
  }
}
