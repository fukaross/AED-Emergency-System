<template>
  <scroll-view class="help-request" :class="themeClass" scroll-y>
    <!-- 当前位置 -->
    <view class="section-card">
      <view class="section-header">
        <text class="section-title">当前位置</text>
        <view class="mini-btn" @click="selectLocation">
          <text class="mini-btn__text">选择位置</text>
        </view>
      </view>
      <input
        v-model="form.address"
        class="input-field"
        placeholder="请选择地图位置后补充楼层/房间"
      />
      <text class="input-hint">请在地图中选点，必要时补充楼层/房间</text>
    </view>

    <!-- 快速操作 -->
    <view class="section-card">
      <text class="section-title">快捷操作</text>
      <view class="quick-grid">
        <view class="quick-btn" @click="quickHelpHand">
          <text class="quick-btn__text">找人手</text>
        </view>
        <view class="quick-btn" @click="quickCall120">
          <text class="quick-btn__text">找 120</text>
        </view>
        <view class="quick-btn" @click="quickCallSchool">
          <text class="quick-btn__text">找校方</text>
        </view>
        <view class="quick-btn" @click="selectLocation">
          <text class="quick-btn__text">重新选点</text>
        </view>
      </view>
    </view>

    <!-- 现场情况 -->
    <view class="section-card">
      <text class="section-title">现场情况</text>
      <textarea
        v-model="form.content"
        class="textarea-field"
        maxlength="200"
        placeholder="请简要描述现场情况，例如：有人晕倒、呼吸微弱、地点在 XX 楼梯口"
      />
      <view class="section-actions">
        <view class="mini-btn" @click="addImage">
          <text class="mini-btn__text">
           上传图片（{{ form.images.length }}/3）
          </text>
        </view>
      </view>
    </view>

    <!-- 联系电话 -->
    <view class="section-card">
      <view class="section-header">
        <text class="section-title">联系电话</text>
        <button
          class="mini-btn phone-btn"
          open-type="getPhoneNumber"
          :loading="isBindingPhone"
          @getphonenumber="onGetPhoneNumber"
        >
          <text class="mini-btn__text">
            {{ phone ? '已授权' : '授权电话' }}
          </text>
        </button>
      </view>
      <text class="input-hint">
        {{ phone || '用于报警通知' }}
      </text>
      <view class="phone-manual">
        <input
          v-model="manualPhone"
          class="input-field phone-input"
          maxlength="11"
          type="number"
          placeholder="手动填写手机号"
        />
        <view
          class="mini-btn phone-save"
          :class="{ 'is-disabled': isBindingPhone }"
          @click="onManualPhoneSave"
        >
          <text class="mini-btn__text">保存电话</text>
        </view>
      </view>
    </view>

    <!-- 隐私同意 + 提交 -->
    <view class="section-card">
      <view class="privacy-row" @click="togglePrivacy">
        <view
          class="privacy-checkbox"
          :class="{ 'is-checked': form.privacyConsent }"
        >
          <text v-if="form.privacyConsent" class="privacy-checkbox__tick">
            ✓
          </text>
        </view>
        <text class="privacy-text">我已阅读并同意紧急求助隐私说明</text>
      </view>
      <view
        class="submit-btn"
        :class="{ 'is-disabled': isSubmitting }"
        @click="submitHelp"
      >
        <text class="submit-btn__text">
          {{ isSubmitting ? '提交中...' : '发布求助' }}
        </text>
      </view>
    </view>

    <!-- 发布结果 -->
    <view v-if="publishResult" class="result-card">
      <text class="result-title">发布成功</text>
      <text class="result-line">编号：{{ publishResult.requestNo }}</text>
      <text v-if="publishResult.followupCode" class="result-line">
        查询码：{{ publishResult.followupCode }}
      </text>
      <text class="result-hint">请妥善保管以上信息，用于后续查询</text>
    </view>

    <!-- 底部安全区 -->
    <view class="safe-bottom" />
  </scroll-view>
</template>

<script setup lang="ts">
/**
 * 寻求帮助页（独立子页面，含四合一快捷操作）
 *
 * 表单区：当前位置（地图选点后可编辑）+ 帮助内容 + 现场图片
 * 快捷操作：找人手 / 找120 / 找校方 / 重新选点
 * 支持游客匿名会话，成功后返回 requestNo / followupCode / closeToken
 */
import { useEmergencyHelp } from '@/composables/useEmergencyHelp'
import { useTheme } from '@/composables/useTheme'

const {
  form,
  isSubmitting,
  isBindingPhone,
  phone,
  manualPhone,
  publishResult,
  selectLocation,
  quickHelpHand,
  quickCall120,
  quickCallSchool,
  addImage,
  bindPhone,
  bindManualPhone,
  submit,
} = useEmergencyHelp()
const { themeClass } = useTheme()

interface PhoneNumberEvent {
  readonly detail: {
    readonly code?: string
    readonly errMsg?: string
  }
}

function togglePrivacy(): void {
  form.privacyConsent = !form.privacyConsent
}

async function onGetPhoneNumber(event: PhoneNumberEvent): Promise<void> {
  if (event.detail.errMsg && !event.detail.code) {
    uni.showToast({ title: '授权失败', icon: 'none' })
    return
  }

  await bindPhone(event.detail.code)
}

async function onManualPhoneSave(): Promise<void> {
  if (isBindingPhone.value) return
  await bindManualPhone()
}

async function submitHelp(): Promise<void> {
  if (isSubmitting.value) return
  const result = await submit()
  if (result) {
    uni.showToast({ title: '求助已发布', icon: 'success' })
  }
}
</script>

<style lang="scss" scoped>
.help-request {
  min-height: 100vh;
  padding: $spacing-md;
  background-color: $background;
  box-sizing: border-box;
}

.section-card {
  margin-bottom: $spacing-md;
  padding: $spacing-md;
  border-radius: $radius-lg;
  background-color: $background-white;
  box-shadow: $shadow-sm;
}

.section-header {
  @include flex-between;
  margin-bottom: $spacing-sm;
}

.section-title {
  display: block;
  margin-bottom: $spacing-sm;
  font-size: $font-size-lg;
  color: $text-primary;
  font-weight: $font-weight-semibold;
}

// 输入框
.input-field {
  width: 100%;
  height: 80rpx;
  line-height: 80rpx;
  background-color: $background;
  border-radius: $radius-md;
  padding: 0 $spacing-md;
  box-sizing: border-box;
  font-size: $font-size-base;
  color: $text-primary;
}

.input-hint {
  display: block;
  margin-top: $spacing-xs;
  font-size: $font-size-xs;
  color: $text-tertiary;
}

// 文本域
.textarea-field {
  width: 100%;
  min-height: 200rpx;
  background-color: $background;
  border-radius: $radius-md;
  padding: $spacing-sm $spacing-md;
  box-sizing: border-box;
  font-size: $font-size-base;
  color: $text-primary;
  line-height: $line-height-normal;
}

// 快捷操作网格
.quick-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: $spacing-sm;
}

.quick-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100rpx;
  border-radius: $radius-md;
  border: 1rpx solid $divider;
  background-color: $background;

  &__text {
    font-size: $font-size-base;
    color: $text-primary;
    font-weight: $font-weight-medium;
  }
}

// 操作按钮
.section-actions {
  margin-top: $spacing-sm;
}

.mini-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 64rpx;
  padding: 0 $spacing-md;
  border-radius: $radius-pill;
  border: 1rpx solid $primary;
  background-color: $background-white;
  line-height: 64rpx;

  &__text {
    font-size: $font-size-sm;
    color: $primary;
    line-height: 64rpx;
  }
}

.phone-btn {
  margin: 0;
}

.phone-btn::after {
  border: 0;
}

.phone-manual {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  margin-top: $spacing-sm;
}

.phone-input {
  flex: 1;
  min-width: 0;
}

.phone-save {
  flex-shrink: 0;

  &.is-disabled {
    opacity: 0.6;
    pointer-events: none;
  }
}

// 隐私勾选行
.privacy-row {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  margin-bottom: $spacing-md;
}

.privacy-checkbox {
  flex-shrink: 0;
  width: 40rpx;
  height: 40rpx;
  border-radius: $radius-sm;
  border: 2rpx solid $text-tertiary;
  @include flex-center;

  &.is-checked {
    background-color: $primary;
    border-color: $primary;
  }

  &__tick {
    font-size: 24rpx;
    color: $text-white;
    font-weight: $font-weight-bold;
  }
}

.privacy-text {
  font-size: $font-size-sm;
  color: $text-secondary;
  line-height: $line-height-normal;
}

// 提交按钮
.submit-btn {
  @include btn-primary;
  width: 100%;

  &.is-disabled {
    background-color: $primary-light;
    pointer-events: none;
  }

  &__text {
    font-size: $font-size-lg;
    color: $text-white;
    font-weight: $font-weight-bold;
  }
}

// 结果卡片
.result-card {
  margin-bottom: $spacing-md;
  padding: $spacing-md;
  border-radius: $radius-lg;
  background-color: $background-white;
  border: 2rpx solid $status-online;
}

.result-title {
  display: block;
  margin-bottom: $spacing-sm;
  color: $status-online;
  font-size: $font-size-lg;
  font-weight: $font-weight-bold;
}

.result-line {
  display: block;
  color: $text-primary;
  font-size: $font-size-base;
  margin-bottom: $spacing-xs;
  line-height: $line-height-normal;
}

.result-actions {
  display: flex;
  flex-wrap: wrap;
  gap: $spacing-sm;
  margin-top: $spacing-sm;
}

.result-hint {
  display: block;
  margin-top: $spacing-sm;
  font-size: $font-size-xs;
  color: $text-tertiary;
}

// 底部安全区
.safe-bottom {
  height: calc(#{$spacing-xl} + env(safe-area-inset-bottom));
}
</style>
