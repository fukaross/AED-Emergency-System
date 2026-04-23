<template>
  <Modal
    v-model="visible"
    title="急救员详情"
    :show-footer="false"
    :show-close="true"
    @after-close="emit('after-close')"
  >
    <view v-if="detail" class="detail-body">
      <view class="detail-row">
        <text class="detail-label">姓名</text>
        <text class="detail-val">{{ displayName }}</text>
      </view>
      <view class="detail-row">
        <text class="detail-label">电话</text>
        <text class="detail-val">{{ detail.phone }}</text>
      </view>
      <view class="detail-row">
        <text class="detail-label">CPR</text>
        <text class="detail-val" :class="detail.skills.cpr ? 'cert-yes' : 'cert-no'">
          {{ detail.skills.cpr ? '已认证' : '未认证' }}
        </text>
      </view>
      <view class="detail-row">
        <text class="detail-label">AED</text>
        <text class="detail-val" :class="detail.skills.aed ? 'cert-yes' : 'cert-no'">
          {{ detail.skills.aed ? '已认证' : '未认证' }}
        </text>
      </view>
      <view class="detail-row">
        <text class="detail-label">STB</text>
        <text class="detail-val" :class="detail.skills.stb ? 'cert-yes' : 'cert-no'">
          {{ detail.skills.stb ? '已认证' : '未认证' }}
        </text>
      </view>
      <view class="detail-row">
        <text class="detail-label">服务校区</text>
        <text class="detail-val">{{ detail.campusName }}</text>
      </view>
      <view v-if="detail.certificateNoMasked" class="detail-row">
        <text class="detail-label">证书编号</text>
        <text class="detail-val">{{ detail.certificateNoMasked }}</text>
      </view>
    </view>

    <template #footer>
      <view class="detail-footer">
        <view class="detail-btn detail-btn--close" @click="emit('close')">
          <text>关闭</text>
        </view>
        <view class="detail-btn detail-btn--call" @click="emit('call')">
          <text>呼叫</text>
        </view>
      </view>
    </template>
  </Modal>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { RescuerDetail } from '@/types/rescuer'
import Modal from '@/components/modal/index.vue'

interface Props {
  modelValue: boolean
  detail: RescuerDetail | null
  name?: string
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  close: []
  call: []
  'after-close': []
}>()

const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val),
})

const displayName = computed(() => {
  if (props.name) {
    return props.name
  }

  return props.detail?.name ?? ''
})
</script>

<style lang="scss" scoped>
.detail-body {
  padding: $spacing-xs 0;
}

.detail-row {
  @include flex-between;
  padding: $spacing-xs 0;
}

.detail-label {
  font-size: $font-size-base;
  color: $text-tertiary;
}

.detail-val {
  font-size: $font-size-base;
  color: $text-primary;
  text-align: right;
  flex: 1;
  margin-left: $spacing-sm;
}

.cert-yes {
  color: $status-online;
}

.cert-no {
  color: $status-offline;
}

.detail-footer {
  @include flex-between;
  gap: $spacing-sm;
  padding: $spacing-sm 0 $spacing-md;
}

.detail-btn {
  flex: 1;
  height: 80rpx;
  border-radius: 40rpx;
  @include flex-center;
  font-size: $font-size-lg;
}

.detail-btn--close {
  background: $background;
  color: $text-secondary;
}

.detail-btn--call {
  background: $primary;
  color: $text-white;
  font-weight: $font-weight-bold;

  &:active {
    background: $primary-dark;
  }
}
</style>
