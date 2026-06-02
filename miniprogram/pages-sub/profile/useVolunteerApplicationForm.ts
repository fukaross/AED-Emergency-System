import { reactive } from 'vue'
import { uploadImage } from '@/api/common'
import { submitVolunteerApplication } from '@/api/user'
import type { Gender } from '@/types/user'
import type {
  VolunteerApplicationPayload,
  VolunteerImageAttachment,
} from '@/types/volunteer'
import {
  MULTI_IMAGE_FIELDS,
  SELECT_FIELDS,
  SINGLE_IMAGE_FIELDS,
  TEXT_FIELDS,
  type MultiImageKey,
  type SelectKey,
  type SingleImageKey,
  type TextKey,
} from './volunteer-fields'

const PHONE_PATTERN = /^1[3-9]\d{9}$/
const ID_NUMBER_PATTERN = /^\d{17}[\dXx]$/
const ID_NUMBER_WEIGHTS = [7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2]
const ID_NUMBER_CHECK_MAP = ['1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2']
const MAX_IMAGE_COUNT = 3
const NAVIGATE_BACK_DELAY_MS = 600

interface VolunteerApplicationForm {
  readonly text: Record<TextKey, string>
  readonly selects: Record<SelectKey, string>
  birthDate: string
  readonly singleImages: Record<SingleImageKey, VolunteerImageAttachment | null>
  readonly multiImages: Record<MultiImageKey, VolunteerImageAttachment[]>
  commitmentAccepted: boolean
}

function createForm(): VolunteerApplicationForm {
  return reactive({
    text: {
      name: '',
      phone: '',
      email: '',
      emergencyContactName: '',
      emergencyContactPhone: '',
      idNumber: '',
      homeAddress: '',
      personalResume: '',
      joinReason: '',
    },
    selects: {
      gender: '',
      politicalStatus: '',
      education: '',
      occupation: '',
      professionalTitle: '',
    },
    birthDate: '',
    singleImages: { idPhoto: null, idCardFront: null, idCardBack: null },
    multiImages: { qualificationImages: [], volunteerCertificateImages: [] },
    commitmentAccepted: false,
  })
}

function getInputValue(event: Event): string {
  return (event as Event & { detail: { value: string } }).detail.value
}

function toAttachment(path: string): VolunteerImageAttachment {
  return { url: path, localPath: path }
}

function isPersistedImageUrl(url: string): boolean {
  return url.startsWith('/uploads/')
    || url.startsWith('http://')
    || url.startsWith('https://')
}

function isValidIdNumber(value: string): boolean {
  const trimmed = value.trim()
  if (!ID_NUMBER_PATTERN.test(trimmed)) return false
  const sum = ID_NUMBER_WEIGHTS.reduce(
    (total, weight, index) => total + Number(trimmed[index]) * weight,
    0,
  )
  return trimmed[17].toUpperCase() === ID_NUMBER_CHECK_MAP[sum % 11]
}

export function useVolunteerApplicationForm() {
  const form = createForm()
  const submitting = reactive({ value: false })

  function setTextValue(key: TextKey, event: Event): void {
    form.text[key] = getInputValue(event)
  }

  function getSelectIndex(key: SelectKey): number {
    return SELECT_FIELDS.find((field) => field.key === key)
      ?.options.findIndex((option) => option.value === form.selects[key]) ?? -1
  }

  function setSelectValue(key: SelectKey, event: Event): void {
    const value = Number(getInputValue(event))
    const field = SELECT_FIELDS.find((item) => item.key === key)
    form.selects[key] = field?.options[value]?.value ?? ''
  }

  function setBirthDate(event: Event): void {
    form.birthDate = getInputValue(event)
  }

  function chooseImagePath(): Promise<string | null> {
    return new Promise((resolve) => {
      uni.chooseImage({
        count: 1,
        sizeType: ['compressed'],
        success: (res) => resolve(res.tempFilePaths[0] ?? null),
        fail: () => {
          uni.showToast({ title: '选择失败', icon: 'none' })
          resolve(null)
        },
      })
    })
  }

  async function chooseSingleImage(key: SingleImageKey): Promise<void> {
    const path = await chooseImagePath()
    if (path) form.singleImages[key] = toAttachment(path)
  }

  async function chooseMultiImage(key: MultiImageKey): Promise<void> {
    if (form.multiImages[key].length >= MAX_IMAGE_COUNT) {
      uni.showToast({ title: '最多3张', icon: 'none' })
      return
    }
    const path = await chooseImagePath()
    if (path) form.multiImages[key].push(toAttachment(path))
  }

  function removeMultiImage(key: MultiImageKey, index: number): void {
    form.multiImages[key].splice(index, 1)
  }

  function toggleCommitment(): void {
    form.commitmentAccepted = !form.commitmentAccepted
  }

  function validateRequired(): boolean {
    const missed = TEXT_FIELDS.find((field) => field.required && !form.text[field.key].trim())
    if (missed) uni.showToast({ title: `请填写${missed.label}`, icon: 'none' })
    return !missed
  }

  function validateSelections(): boolean {
    if (!form.birthDate) uni.showToast({ title: '请选择出生日期', icon: 'none' })
    if (!form.birthDate) return false
    const missed = SELECT_FIELDS.find((field) => !form.selects[field.key])
    if (missed) uni.showToast({ title: missed.placeholder, icon: 'none' })
    return !missed
  }

  function validateImages(): boolean {
    const singleMissed = SINGLE_IMAGE_FIELDS.find((field) => !form.singleImages[field.key])
    if (singleMissed) uni.showToast({ title: `请上传${singleMissed.hint}`, icon: 'none' })
    if (singleMissed) return false
    const multiMissed = MULTI_IMAGE_FIELDS.find((field) => form.multiImages[field.key].length === 0)
    if (multiMissed) uni.showToast({ title: `请上传${multiMissed.label}`, icon: 'none' })
    return !multiMissed
  }

  function validateFormats(): boolean {
    if (!PHONE_PATTERN.test(form.text.phone)) {
      uni.showToast({ title: '联系电话有误', icon: 'none' })
      return false
    }
    if (!PHONE_PATTERN.test(form.text.emergencyContactPhone)) {
      uni.showToast({ title: '紧急电话有误', icon: 'none' })
      return false
    }
    if (!isValidIdNumber(form.text.idNumber)) {
      uni.showToast({ title: '身份证号有误', icon: 'none' })
      return false
    }
    return true
  }

  function validateForm(): boolean {
    if (!validateRequired() || !validateSelections()) return false
    if (!validateImages() || !validateFormats()) return false
    if (form.commitmentAccepted) return true
    uni.showToast({ title: '请勾选承诺', icon: 'none' })
    return false
  }

  async function normalizeAttachment(
    item: VolunteerImageAttachment,
  ): Promise<VolunteerImageAttachment> {
    if (isPersistedImageUrl(item.url)) return item
    const uploaded = await uploadImage(item.localPath || item.url, 'volunteer')
    return {
      url: uploaded.url,
      fileId: uploaded.fileId,
      name: item.name,
    }
  }

  async function normalizeAttachments(
    items: VolunteerImageAttachment[],
  ): Promise<VolunteerImageAttachment[]> {
    const uploaded: VolunteerImageAttachment[] = []
    for (const item of items) {
      uploaded.push(await normalizeAttachment(item))
    }
    return uploaded
  }

  async function buildPayload(): Promise<VolunteerApplicationPayload> {
    return {
      ...form.text,
      gender: form.selects.gender as Gender,
      birthDate: form.birthDate,
      politicalStatus: form.selects.politicalStatus,
      education: form.selects.education,
      occupation: form.selects.occupation,
      professionalTitle: form.selects.professionalTitle,
      idPhoto: await normalizeAttachment(form.singleImages.idPhoto!),
      idCardFront: await normalizeAttachment(form.singleImages.idCardFront!),
      idCardBack: await normalizeAttachment(form.singleImages.idCardBack!),
      qualificationImages: await normalizeAttachments(form.multiImages.qualificationImages),
      volunteerCertificateImages: await normalizeAttachments(form.multiImages.volunteerCertificateImages),
      commitmentAccepted: form.commitmentAccepted,
    }
  }

  async function submitForm(): Promise<void> {
    if (submitting.value || !validateForm()) return
    submitting.value = true
    try {
      await submitVolunteerApplication(await buildPayload())
      uni.showToast({ title: '提交成功', icon: 'success' })
      setTimeout(() => uni.navigateBack(), NAVIGATE_BACK_DELAY_MS)
    } catch (error) {
      console.error('[Volunteer] 提交失败', error)
      uni.showToast({ title: '提交失败', icon: 'none' })
    } finally {
      submitting.value = false
    }
  }

  return {
    form,
    submitting,
    chooseMultiImage,
    chooseSingleImage,
    getSelectIndex,
    removeMultiImage,
    setBirthDate,
    setSelectValue,
    setTextValue,
    submitForm,
    toggleCommitment,
  }
}
