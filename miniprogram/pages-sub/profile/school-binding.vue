<template>
  <view class="id-page" :class="themeClass">
    <custom-navbar title="学校绑定" :show-back="true" :back-icon="BACK_ICON" />

    <scroll-view class="id-scroll" scroll-y>
      <view class="id-card">
        <view class="id-row">
          <text class="id-label id-label--required">身份</text>
          <picker :range="PERSON_TYPES" range-key="label" :value="personTypeIndex" @change="setPersonType">
            <view class="id-picker">{{ currentPersonTypeLabel }}</view>
          </picker>
        </view>
        <view class="id-row">
          <text class="id-label id-label--required">姓名</text>
          <input v-model="form.name" class="id-input" placeholder="请输入姓名" maxlength="40" />
        </view>
        <view class="id-row">
          <text class="id-label id-label--required">学院</text>
          <input v-model="form.college" class="id-input" placeholder="请输入学院" maxlength="40" />
        </view>
        <view class="id-row">
          <text class="id-label">班级</text>
          <input v-model="form.className" class="id-input" placeholder="选填" maxlength="40" />
        </view>
        <view class="id-row">
          <text class="id-label" :class="{ 'id-label--required': form.personType === 'student' }">学号</text>
          <input v-model="form.studentId" class="id-input" placeholder="学生必填" maxlength="32" />
        </view>
        <view class="id-row">
          <text class="id-label" :class="{ 'id-label--required': form.personType !== 'student' }">工号</text>
          <input v-model="form.staffId" class="id-input" placeholder="教职工必填" maxlength="32" />
        </view>
        <view class="id-row">
          <text class="id-label">手机号</text>
          <input v-model="form.phone" class="id-input" type="number" placeholder="选填" maxlength="11" />
        </view>
        <textarea v-model="form.note" class="id-textarea" placeholder="补充说明" maxlength="200" />
      </view>

      <view class="id-card">
        <text class="id-list-title">申请记录</text>
        <text v-if="applications.length === 0" class="id-empty">暂无记录</text>
        <view v-for="item in applications" :key="item.applicationId" class="id-status-item">
          <view class="id-status-line">
            <text class="id-status-name">{{ item.payload.name }} · {{ item.payload.college }}</text>
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
import { computed, reactive, ref } from 'vue'
import CustomNavbar from '@/components/custom-navbar/index.vue'
import { useTheme } from '@/composables/useTheme'
import { fetchSchoolBindings, submitSchoolBinding } from '@/api/user'
import type { IdentityApplicationStatus, SchoolBindingDetail, SchoolPersonType } from '@/types/user'

const BACK_ICON = '/static/icons/ico-left-arrow.svg'

const PERSON_TYPES: readonly { label: string; value: SchoolPersonType }[] = [
  { label: '学生', value: 'student' },
  { label: '教师', value: 'teacher' },
  { label: '职工', value: 'staff' },
]

const { themeClass } = useTheme()
const submitting = ref(false)
const applications = ref<readonly SchoolBindingDetail[]>([])
const form = reactive({
  personType: 'student' as SchoolPersonType,
  name: '',
  college: '',
  className: '',
  studentId: '',
  staffId: '',
  phone: '',
  note: '',
})

const personTypeIndex = computed(() => PERSON_TYPES.findIndex((item) => item.value === form.personType))
const currentPersonTypeLabel = computed(() => PERSON_TYPES[personTypeIndex.value]?.label ?? '学生')

function setPersonType(event: { detail: { value: string | number } }): void {
  const index = Number(event.detail.value)
  form.personType = PERSON_TYPES[index]?.value ?? 'student'
}

async function loadApplications(): Promise<void> {
  const result = await fetchSchoolBindings()
  applications.value = result.items
}

async function submit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  try {
    await submitSchoolBinding({ ...form })
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
