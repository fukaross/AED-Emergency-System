<template>
  <BottomSheet
    :model-value="modelValue"
    title="AED 问题报告"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <view class="rf">
      <text class="rf-label">选择设备</text>
      <picker :range="deviceNames" :value="devIdx" @change="onDeviceChange">
        <view class="rf-picker">
          <text class="rf-picker-text">{{ deviceNames[devIdx] ?? '请选择' }}</text>
          <text class="rf-picker-arrow">▼</text>
        </view>
      </picker>

      <text class="rf-label">问题类型</text>
      <view class="rf-types">
        <view
          v-for="item in REPORT_TYPE_OPTIONS"
          :key="item.value"
          class="rf-type"
          :class="{ 'rf-type--active': reportType === item.value }"
          @click="reportType = item.value"
        >
          <view class="rf-type-radio" />
          <text class="rf-type-text">{{ item.label }}</text>
        </view>
      </view>

      <text class="rf-label">问题描述</text>
      <textarea
        class="rf-textarea"
        :value="description"
        placeholder="请描述遇到的问题..."
        :maxlength="200"
        @input="onDescInput"
      />

      <text class="rf-label">现场图片（可选）</text>
      <view class="rf-imgs">
        <view v-for="(img, i) in images" :key="i" class="rf-img-item">
          <image :src="img" mode="aspectFill" class="rf-img-preview" />
          <view class="rf-img-remove" @click="removeImage(i)">
            <text class="rf-img-remove-icon">✕</text>
          </view>
        </view>
        <view
          v-if="images.length < MAX_IMAGES"
          class="rf-img-add"
          @click="chooseImage"
        >
          <image :src="CAMERA_ICON" class="rf-img-camera" mode="aspectFit" />
        </view>
      </view>

      <template v-if="requireContact">
        <text class="rf-label">联系方式（至少填写一项）</text>
        <input
          class="rf-input"
          :value="contactPhone"
          type="number"
          maxlength="11"
          placeholder="手机号"
          @input="onPhoneInput"
        />
        <input
          class="rf-input"
          :value="contactEmail"
          maxlength="50"
          placeholder="邮箱"
          @input="onEmailInput"
        />
      </template>

      <view
        class="rf-submit"
        :class="{ 'is-disabled': !canSubmit }"
        @click="handleSubmit"
      >
        <text>提交报告</text>
      </view>
    </view>
  </BottomSheet>
</template>

<script setup lang="ts">
// AED 问题报告表单 —— 选设备 + 问题类型 + 描述 + 图片，关闭自动重置
import { ref, computed, watch } from 'vue'
import BottomSheet from '@/components/bottom-sheet/index.vue'
import type { AedReportType } from '@/types/aed'
import { MAX_REPORT_IMAGES } from '@/utils/constants'

const CAMERA_ICON = '/static/icons/camera.svg'
const MAX_IMAGES = MAX_REPORT_IMAGES

const REPORT_TYPE_OPTIONS: readonly { value: AedReportType; label: string }[] = [
  { value: 'damaged', label: '设备损坏' },
  { value: 'relocated', label: '位置变动' },
  { value: 'unusable', label: '无法使用' },
  { value: 'other', label: '其他' },
]

const props = defineProps<{
  modelValue: boolean
  devices: readonly { id: string; name: string }[]
  requireContact?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  submit: [payload: {
    deviceId: string
    type: AedReportType
    description: string
    images: string[]
    contactPhone?: string
    contactEmail?: string
  }]
}>()

const devIdx = ref(0)
const reportType = ref<AedReportType>('damaged')
const description = ref('')
const images = ref<string[]>([])
const contactPhone = ref('')
const contactEmail = ref('')

const deviceNames = computed(() => props.devices.map(d => d.name))
const canSubmit = computed(() => {
  if (!description.value.trim()) {
    return false
  }

  if (!props.requireContact) {
    return true
  }

  return Boolean(contactPhone.value.trim() || contactEmail.value.trim())
})

// 关闭时重置表单
watch(() => props.modelValue, (visible) => {
  if (!visible) {
    devIdx.value = 0
    reportType.value = 'damaged'
    description.value = ''
    images.value = []
    contactPhone.value = ''
    contactEmail.value = ''
  }
})

function onDeviceChange(e: { detail: { value: number } }) {
  devIdx.value = e.detail.value
}

function onDescInput(e: InputEvent) {
  const detail = e.detail as unknown as { value?: string }
  description.value = detail.value ?? ''
}

function onPhoneInput(e: InputEvent) {
  const detail = e.detail as unknown as { value?: string }
  contactPhone.value = detail.value ?? ''
}

function onEmailInput(e: InputEvent) {
  const detail = e.detail as unknown as { value?: string }
  contactEmail.value = detail.value ?? ''
}

function chooseImage() {
  const remaining = MAX_IMAGES - images.value.length
  if (remaining <= 0) return
  uni.chooseImage({
    count: remaining,
    sizeType: ['compressed'],
    success: (res) => { images.value.push(...res.tempFilePaths) },
  })
}

function removeImage(index: number) {
  images.value.splice(index, 1)
}

function handleSubmit() {
  if (!canSubmit.value) {
    uni.showToast({
      title: props.requireContact ? '请填写描述和联系方式' : '请填写问题描述',
      icon: 'none',
    })
    return
  }
  const device = props.devices[devIdx.value]
  if (!device) return
  emit('submit', {
    deviceId: device.id,
    type: reportType.value,
    description: description.value.trim(),
    images: [...images.value],
    contactPhone: contactPhone.value.trim() || undefined,
    contactEmail: contactEmail.value.trim() || undefined,
  })
}
</script>

<style lang="scss" scoped>
.rf { padding-bottom: $spacing-md; }

.rf-label {
  display: block;
  font-size: $font-size-base;
  font-weight: $font-weight-medium;
  color: $text-primary;
  margin: $spacing-md 0 $spacing-xs;

  &:first-child { margin-top: 0; }
}

.rf-picker {
  @include flex-between;
  height: 80rpx;
  padding: 0 $spacing-md;
  background: $background;
  border-radius: $radius-md;
}

.rf-picker-text { font-size: $font-size-base; color: $text-primary; }
.rf-picker-arrow { font-size: $font-size-sm; color: $text-tertiary; }

.rf-types { display: flex; flex-wrap: wrap; gap: $spacing-sm; }

.rf-type {
  @include flex-align-center;
  gap: $spacing-xs;
  padding: $spacing-xs $spacing-sm;
  border-radius: $radius-pill;
  background: $background;
}

.rf-type-radio {
  width: 20rpx;
  height: 20rpx;
  border-radius: $radius-full;
  border: 2rpx solid $text-tertiary;
}

.rf-type-text { font-size: $font-size-sm; color: $text-secondary; }

.rf-type--active {
  background: rgba(229, 57, 53, 0.1);

  .rf-type-radio { border-color: $primary; background: $primary; }
  .rf-type-text { color: $primary; }
}

.rf-textarea {
  width: 100%;
  min-height: 160rpx;
  padding: $spacing-sm;
  background: $background;
  border-radius: $radius-md;
  font-size: $font-size-base;
  color: $text-primary;
  box-sizing: border-box;
}

.rf-input {
  width: 100%;
  height: 80rpx;
  margin-top: $spacing-sm;
  padding: 0 $spacing-sm;
  background: $background;
  border-radius: $radius-md;
  font-size: $font-size-base;
  color: $text-primary;
  box-sizing: border-box;
}

.rf-imgs { display: flex; flex-wrap: wrap; gap: $spacing-sm; }

.rf-img-item {
  position: relative;
  width: 160rpx;
  height: 160rpx;
  border-radius: $radius-md;
  overflow: hidden;
}

.rf-img-preview { width: 100%; height: 100%; }

.rf-img-remove {
  position: absolute;
  top: 4rpx;
  right: 4rpx;
  width: 36rpx;
  height: 36rpx;
  background: $overlay-mask;
  border-radius: $radius-full;
  @include flex-center;
}

.rf-img-remove-icon { color: $text-white; font-size: $font-size-xs; }

.rf-img-add {
  width: 160rpx;
  height: 160rpx;
  border-radius: $radius-md;
  border: 2rpx dashed $divider;
  background: $background;
  @include flex-center;
}

.rf-img-camera { width: 48rpx; height: 48rpx; opacity: 0.4; }

.rf-submit { @include btn-primary; margin-top: $spacing-lg; }
</style>
