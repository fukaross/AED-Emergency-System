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

      <view class="image-grid">
        <view
          v-for="(image, index) in images"
          :key="image"
          class="image-item"
          @click="previewImage(index)"
        >
          <image class="report-image" :src="image" mode="aspectFill" />
        </view>
        <view v-if="images.length < maxImages" class="upload-card" @click="addImages">
          <view class="upload-icon">
            <text class="upload-icon__plus">+</text>
          </view>
          <text class="upload-text">上传图片</text>
        </view>
      </view>

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

const MAX_REPORT_IMAGES = 6

const props = defineProps<{
  readonly modelValue: boolean
  readonly submitting: boolean
}>()

const emit = defineEmits<{
  (event: 'update:modelValue', value: boolean): void
  (event: 'submit', payload: SubmitHelpReportPayload): void
}>()

const images = ref<string[]>([])
const recordText = ref('')
const isFalseAlarm = ref(false)
const maxImages = MAX_REPORT_IMAGES

watch(
  () => props.modelValue,
  (visible) => {
    if (!visible) return
    images.value = []
    recordText.value = ''
    isFalseAlarm.value = false
  },
)

function close(): void {
  if (props.submitting) return
  emit('update:modelValue', false)
}

async function addImages(): Promise<void> {
  if (images.value.length >= MAX_REPORT_IMAGES) return

  const res = await uni.chooseImage({
    count: MAX_REPORT_IMAGES - images.value.length,
    sizeType: ['compressed'],
  })

  for (const path of res.tempFilePaths) {
    const uploaded = await uploadImage(path, 'report')
    images.value.push(uploaded.url)
  }
}

function previewImage(index: number): void {
  uni.previewImage({ current: images.value[index], urls: images.value })
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
  if (!recordText.value.trim()) {
    uni.showToast({ title: '请填写记录', icon: 'none' })
    return
  }

  emit('submit', {
    images: images.value,
    recordText: recordText.value.trim(),
    isFalseAlarm: isFalseAlarm.value,
  })
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

.image-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: $spacing-sm;
  margin-bottom: $spacing-md;
}

.image-item,
.upload-card {
  aspect-ratio: 1;
  border-radius: $radius-md;
  overflow: hidden;
}

.report-image {
  width: 100%;
  height: 100%;
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
