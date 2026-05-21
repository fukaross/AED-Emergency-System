<template>
  <view v-if="modelValue" class="report-mask" @click="close">
    <view class="report-sheet" @click.stop>
      <view class="sheet-header">
        <text class="sheet-title">救助报告</text>
        <view
          class="sheet-close"
          @click="close"
        >
          <text class="sheet-close__icon">×</text>
        </view>
      </view>

      <view class="image-header">
        <text class="image-label">现场图片</text>
        <text class="image-count">{{ images.length }}/{{ maxImages }}</text>
      </view>
      <view class="image-grid">
        <view
          v-for="(image, index) in images"
          :key="image.id"
          class="image-item"
          @click="previewImage(index)"
        >
          <image class="report-image" :src="image.localUrl" mode="aspectFill" />
          <text v-if="image.uploading" class="image-status">上传中</text>
          <text v-else-if="image.failed" class="image-status image-status--error">
            失败
          </text>
          <text class="image-remove" @click.stop="removeImage(image.id)">×</text>
        </view>
        <view v-if="images.length < maxImages" class="upload-card" @click="addImages">
          <view class="upload-icon">
            <text class="upload-icon__plus">+</text>
          </view>
          <text class="upload-text">上传图片</text>
        </view>
      </view>
      <text v-if="uploadErrorMessage" class="image-error">
        {{ uploadErrorMessage }}
      </text>

      <textarea
        v-model="recordText"
        class="record-input"
        maxlength="300"
        placeholder="填写救助记录"
      />

      <view class="false-row" @click="toggleFalseAlarm">
        <view class="checkbox" :class="{ 'is-checked': isFalseAlarm }">
          <text v-if="isFalseAlarm" class="checkbox-mark">✓</text>
        </view>
        <text class="false-text">标记为误报</text>
      </view>

      <view class="submit-btn" :class="{ 'is-disabled': submitting }" @click="submit">
        <text class="submit-text">{{ submitting ? '提交中...' : '提交报告' }}</text>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { uploadImage } from '@/api/common'
import type { SubmitHelpReportPayload } from '@/types/help'
import { MAX_REPORT_IMAGES } from '@/utils/constants'

const props = defineProps<{
  readonly modelValue: boolean
  readonly submitting: boolean
}>()

const emit = defineEmits<{
  (event: 'update:modelValue', value: boolean): void
  (event: 'submit', payload: SubmitHelpReportPayload): void
}>()

interface ReportImageEntry {
  readonly id: string
  readonly localUrl: string
  readonly remoteUrl?: string
  readonly uploading: boolean
  readonly failed: boolean
}

const images = ref<ReportImageEntry[]>([])
const recordText = ref('')
const isFalseAlarm = ref(false)
const uploadErrorMessage = ref('')
const maxImages = MAX_REPORT_IMAGES

watch(
  () => props.modelValue,
  (visible) => {
    if (!visible) return
    images.value = []
    recordText.value = ''
    isFalseAlarm.value = false
    uploadErrorMessage.value = ''
  },
)

function close(): void {
  if (props.submitting) return
  emit('update:modelValue', false)
}

async function addImages(): Promise<void> {
  const remaining = MAX_REPORT_IMAGES - images.value.length
  if (remaining <= 0) {
    uni.showToast({ title: `最多${MAX_REPORT_IMAGES}张`, icon: 'none' })
    return
  }

  try {
    const res = await uni.chooseImage({ count: remaining, sizeType: ['compressed'] })
    uploadErrorMessage.value = ''
    for (const path of res.tempFilePaths.slice(0, remaining)) {
      const entry = createImageEntry(path)
      images.value = [...images.value, entry]
      void uploadImageEntry(entry)
    }
  } catch (error) {
    if (!isUserCancel(error)) {
      uni.showToast({ title: '选择失败', icon: 'none' })
    }
  }
}

async function uploadImageEntry(entry: ReportImageEntry): Promise<void> {
  try {
    const uploaded = await uploadImage(entry.localUrl, 'report')
    updateImageEntry(entry.id, { remoteUrl: uploaded.url, uploading: false })
  } catch (error) {
    const message = getErrorMessage(error)
    console.warn('[HelpReport] 图片上传失败', error)
    updateImageEntry(entry.id, { uploading: false, failed: true })
    uploadErrorMessage.value = message
    uni.showToast({ title: message, icon: 'none' })
  }
}

function previewImage(index: number): void {
  const urls = images.value.map((item) => item.localUrl)
  uni.previewImage({ current: urls[index], urls })
}

function removeImage(id: string): void {
  images.value = images.value.filter((image) => image.id !== id)
  if (!images.value.some((image) => image.failed)) {
    uploadErrorMessage.value = ''
  }
}

function toggleFalseAlarm(): void {
  isFalseAlarm.value = !isFalseAlarm.value
}

function submit(): void {
  if (props.submitting) return
  if (!images.value.length) {
    uni.showToast({ title: '请上传图片', icon: 'none' })
    return
  }
  if (images.value.some((image) => image.uploading)) {
    uni.showToast({ title: '图片上传中', icon: 'none' })
    return
  }
  if (images.value.some((image) => image.failed || !image.remoteUrl)) {
    uni.showToast({ title: '请重传图片', icon: 'none' })
    return
  }
  if (!recordText.value.trim()) {
    uni.showToast({ title: '请填写记录', icon: 'none' })
    return
  }

  emit('submit', {
    images: images.value.map((image) => image.remoteUrl!),
    recordText: recordText.value.trim(),
    isFalseAlarm: isFalseAlarm.value,
  })
}

function createImageEntry(localUrl: string): ReportImageEntry {
  return {
    id: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
    localUrl,
    uploading: true,
    failed: false,
  }
}

function updateImageEntry(
  id: string,
  patch: Partial<ReportImageEntry>,
): void {
  images.value = images.value.map((image) =>
    image.id === id ? { ...image, ...patch } : image,
  )
}

function isUserCancel(error: unknown): boolean {
  return String((error as { errMsg?: string })?.errMsg || error).includes('cancel')
}

function getErrorMessage(error: unknown): string {
  return error instanceof Error && error.message ? error.message : '上传失败'
}
</script>

<style lang="scss" scoped>
.report-mask {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: flex;
  align-items: flex-end;
  background: $overlay-mask;
}

.report-sheet {
  width: 100%;
  padding: $spacing-md;
  padding-bottom: calc(#{$spacing-md} + env(safe-area-inset-bottom));
  border-radius: $radius-xl $radius-xl 0 0;
  background: $background-white;
  box-sizing: border-box;
}

.sheet-header {
  @include flex-between;
  margin-bottom: $spacing-md;
}

.sheet-title {
  color: $text-primary;
  font-size: $font-size-xl;
  font-weight: $font-weight-bold;
}

.sheet-close {
  @include flex-center;
  width: 40rpx;
  height: 40rpx;
}

.sheet-close__icon {
  color: $text-secondary;
  font-size: 40rpx;
  line-height: 40rpx;
}

.image-header {
  @include flex-between;
  margin-bottom: $spacing-sm;
}

.image-label {
  color: $text-secondary;
  font-size: $font-size-sm;
}

.image-count {
  color: $text-tertiary-a11y;
  font-size: $font-size-sm;
}

.image-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: $spacing-sm;
  margin-bottom: $spacing-md;
}

.image-item,
.upload-card {
  position: relative;
  aspect-ratio: 1;
  border-radius: $radius-md;
  overflow: hidden;
}

.report-image {
  width: 100%;
  height: 100%;
}

.image-status {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  padding: 6rpx 0;
  background: rgb(0 0 0 / 55%);
  color: $text-white;
  font-size: $font-size-xs;
  line-height: 1.4;
  text-align: center;
}

.image-status--error {
  background: rgb(239 68 68 / 80%);
}

.image-remove {
  position: absolute;
  top: 8rpx;
  right: 8rpx;
  width: 36rpx;
  height: 36rpx;
  border-radius: $radius-full;
  background: rgb(0 0 0 / 50%);
  color: $text-white;
  font-size: $font-size-base;
  line-height: 36rpx;
  text-align: center;
}

.image-error {
  display: block;
  margin: -#{$spacing-xs} 0 $spacing-sm;
  color: $primary;
  font-size: $font-size-sm;
}

.upload-card {
  @include flex-column;
  align-items: center;
  justify-content: center;
  background: $background;
  border: 1rpx dashed $divider;
}

.upload-icon {
  @include flex-center;
  width: 48rpx;
  height: 48rpx;
  border: 2rpx solid $text-tertiary-a11y;
  border-radius: $radius-full;
}

.upload-icon__plus {
  color: $text-tertiary-a11y;
  font-size: $font-size-xl;
  line-height: 48rpx;
}

.upload-text {
  margin-top: $spacing-xs;
  color: $text-secondary;
  font-size: $font-size-sm;
}

.record-input {
  width: 100%;
  min-height: 180rpx;
  padding: $spacing-sm;
  border-radius: $radius-md;
  background: $background;
  color: $text-primary;
  font-size: $font-size-base;
  box-sizing: border-box;
}

.false-row {
  @include flex-align-center;
  gap: $spacing-sm;
  margin: $spacing-md 0;
}

.checkbox {
  @include flex-center;
  width: 40rpx;
  height: 40rpx;
  border: 2rpx solid $divider;
  border-radius: $radius-sm;
}

.checkbox.is-checked {
  border-color: $primary;
  background: $primary;
}

.checkbox-mark {
  color: $text-white;
  font-size: $font-size-sm;
}

.false-text {
  color: $text-secondary;
  font-size: $font-size-base;
}

.submit-btn {
  @include btn-primary;
}

.submit-btn.is-disabled {
  opacity: 0.7;
}

.submit-text {
  color: $text-white;
  font-size: $font-size-lg;
  font-weight: $font-weight-bold;
}
</style>
