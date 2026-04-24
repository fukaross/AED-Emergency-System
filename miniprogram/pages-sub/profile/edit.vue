<template>
  <view class="pe-root">
    <custom-navbar
      title="个人资料"
      :show-back="true"
      :back-icon="BACK_ICON"
      :before-back="handleBeforeBack"
    />

    <!-- 头像区域 -->
    <view class="pe-avatar-section">
      <view class="pe-avatar-wrap" @tap="handleChooseAvatar">
        <image
          class="pe-avatar-img"
          :src="displayAvatar"
          mode="aspectFill"
        />
        <view class="pe-avatar-camera-bg">
          <image
            class="pe-avatar-camera"
            :src="CAMERA_ICON"
            mode="aspectFit"
          />
        </view>
      </view>
    </view>

    <!-- 表单卡片 -->
    <view class="pe-form">
      <!-- 昵称 -->
      <view class="pe-row">
        <text class="pe-label">昵称</text>
        <input
          v-model="form.name"
          class="pe-input"
          placeholder="请输入昵称"
          :maxlength="20"
        />
      </view>

      <!-- 性别 -->
      <view class="pe-row">
        <text class="pe-label">性别</text>
        <view class="pe-gender-group">
          <view
            v-for="g in GENDER_OPTIONS"
            :key="g.value"
            class="pe-gender-item"
            @tap="form.gender = g.value"
          >
            <view
              class="pe-radio"
              :class="{ 'pe-radio--checked': form.gender === g.value }"
            />
            <text class="pe-gender-label">{{ g.label }}</text>
          </view>
        </view>
      </view>

      <!-- 联系电话 -->
      <view class="pe-row">
        <text class="pe-label">联系电话</text>
        <input
          v-model="form.phone"
          class="pe-input"
          type="number"
          placeholder="请输入手机号"
          :maxlength="11"
          @input="onPhoneInput"
        />
      </view>

      <!-- 学校档案（只读） -->
      <view class="pe-row pe-row--readonly">
        <text class="pe-label">学校档案</text>
        <view class="pe-readonly">
          <template v-if="schoolProfile">
            <text class="pe-readonly-text">{{ schoolProfile.college }}</text>
            <text
              v-if="schoolProfile.studentId || schoolProfile.staffId"
              class="pe-readonly-sub"
            >
              · {{ schoolProfile.studentId || schoolProfile.staffId }}
            </text>
            <text class="pe-readonly-sub">· {{ bindingStatusLabel }}</text>
          </template>
          <text v-else class="pe-readonly-empty">未绑定学校档案</text>
        </view>
      </view>

      <!-- 急救资质（只读） -->
      <view class="pe-row pe-row--readonly pe-row--last">
        <text class="pe-label">急救资质</text>
        <view class="pe-readonly">
          <view v-if="rescuerLevel" class="pe-qual">
            <level-badge :level="rescuerLevel" />
            <text class="pe-lock-icon">🔒</text>
          </view>
          <text v-else class="pe-readonly-empty">暂无急救资质</text>
        </view>
      </view>
    </view>

    <!-- 保存按钮 -->
    <view class="pe-btn-area" :style="{ paddingBottom: safeAreaBottomPx }">
      <view
        class="pe-btn-save"
        :class="{ 'pe-btn-save--disabled': saving }"
        @tap="handleSave"
      >
        <text>{{ saving ? '保存中' : '保存修改' }}</text>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import LevelBadge from '@/components/level-badge/index.vue'
import CustomNavbar from '@/components/custom-navbar/index.vue'
import { useProfileEditForm } from './useProfileEditForm'

const BACK_ICON = '/static/icons/ico-left-arrow.svg'

const {
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
} = useProfileEditForm()
</script>

<style src="./edit.scss" lang="scss" scoped></style>
