<template>
  <view class="id-page" :class="themeClass">
    <custom-navbar title="急救员认领" :show-back="true" :back-icon="BACK_ICON" />

    <scroll-view class="id-scroll" scroll-y>
      <view class="id-card">
        <view class="id-row">
          <text class="id-label id-label--required">姓名</text>
          <input v-model="form.name" class="id-input" placeholder="请输入姓名" maxlength="40" />
        </view>
        <view class="id-row">
          <text class="id-label id-label--required">证件后 6 位</text>
          <input v-model="form.idNumberLast6" class="id-input" placeholder="后 6 位" maxlength="6" />
        </view>
        <view class="id-row">
          <text class="id-label id-label--required">手机号</text>
          <input v-model="form.phone" class="id-input" type="number" placeholder="请输入手机号" maxlength="11" />
        </view>
        <view class="id-row">
          <text class="id-label">证书号</text>
          <input v-model="form.certificateNo" class="id-input" placeholder="选填" maxlength="64" />
        </view>
        <textarea v-model="form.note" class="id-textarea" placeholder="补充说明" maxlength="200" />
      </view>

      <view class="id-card">
        <text class="id-list-title">申请记录</text>
        <text v-if="applications.length === 0" class="id-empty">暂无记录</text>
        <view v-for="item in applications" :key="item.applicationId" class="id-status-item">
          <view class="id-status-line">
            <text class="id-status-name">{{ item.payload.name }} · {{ item.payload.phone }}</text>
            <text class="id-status-pill" :class="statusClass(item.status)">
              {{ statusLabel(item.status) }}
            </text>
          </view>
          <text v-if="item.reviewNote" class="id-status-sub">{{ item.reviewNote }}</text>
        </view>
      </view>

      <view class="id-submit-wrap">
        <view class="id-submit" :class="{ 'id-submit--disabled': submitting }" @tap="submit">
          <text>{{ submitting ? '提交中' : '提交' }}</text>
        </view>
      </view>
    </scroll-view>
  </view>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import CustomNavbar from '@/components/custom-navbar/index.vue'
import { useTheme } from '@/composables/useTheme'
import { fetchResponderClaims, submitResponderClaim } from '@/api/user'
import type { IdentityApplicationStatus, ResponderClaimDetail } from '@/types/user'

const BACK_ICON = '/static/icons/ico-left-arrow.svg'

const { themeClass } = useTheme()
const submitting = ref(false)
const applications = ref<readonly ResponderClaimDetail[]>([])
const form = reactive({
  name: '',
  idNumberLast6: '',
  phone: '',
  certificateNo: '',
  note: '',
})

async function loadApplications(): Promise<void> {
  const result = await fetchResponderClaims()
  applications.value = result.items
}

async function submit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  try {
    await submitResponderClaim({ ...form })
    uni.showToast({ title: '已提交', icon: 'success' })
    await loadApplications()
  } finally {
    submitting.value = false
  }
}

function statusLabel(status: IdentityApplicationStatus): string {
  if (status === 'approved') return '已通过'
  if (status === 'rejected') return '已驳回'
  return '待审核'
}

function statusClass(status: IdentityApplicationStatus): string {
  return status === 'approved' ? 'id-status-pill--approved'
    : status === 'rejected' ? 'id-status-pill--rejected' : ''
}

void loadApplications()
</script>

<style src="./identity.scss" lang="scss" scoped></style>
