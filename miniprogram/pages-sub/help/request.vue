<template>
  <scroll-view class="help-request" scroll-y>
    <view class="section-card">
      <view class="section-header">
        <text class="section-title">当前位置</text>
        <button class="mini-btn" @click="autoLocate">自动定位</button>
      </view>
      <input
        v-model="form.address"
        class="input-field"
        placeholder="请输入详细位置（教学楼/楼层/房间）"
      />
    </view>

    <view class="section-card">
      <text class="section-title">快速操作</text>
      <view class="quick-grid">
        <button class="quick-btn" @click="quickHelpHand">找人手</button>
        <button class="quick-btn" @click="quickCall120">找 120</button>
        <button class="quick-btn" @click="quickCallSchool">找校方</button>
        <button class="quick-btn" @click="autoLocate">重新定位</button>
      </view>
    </view>

    <view class="section-card">
      <text class="section-title">现场情况</text>
      <textarea
        v-model="form.content"
        class="textarea-field"
        maxlength="200"
        placeholder="请简要描述现场情况，例如：有人晕倒、呼吸微弱、地点在 XX 楼梯口"
      />
      <view class="section-actions">
        <button class="mini-btn" @click="addImage">
          上传图片（{{ form.images.length }}/3）
        </button>
      </view>
    </view>

    <view class="section-card">
      <view class="privacy-row">
        <switch :checked="form.privacyConsent" @change="handlePrivacyChange" />
        <text class="privacy-text">我已阅读并同意紧急求助隐私说明</text>
      </view>
      <button
        class="submit-btn"
        :disabled="isSubmitting"
        @click="submitHelp"
      >
        {{ isSubmitting ? '提交中...' : '发布求助' }}
      </button>
    </view>

    <view v-if="publishResult" class="result-card">
      <text class="result-title">发布成功</text>
      <text class="result-line">编号：{{ publishResult.requestNo }}</text>
      <text v-if="publishResult.followupCode" class="result-line">
        查询码：{{ publishResult.followupCode }}
      </text>
    </view>
  </scroll-view>
</template>

<script setup lang="ts">
/**
 * 寻求帮助页（独立子页面，含四合一快捷操作）
 *
 * [G-01] 本页面为帮助能力唯一入口，地图页不做帮助弹窗。
 *
 * 表单区：当前位置（自动定位可编辑）+ 帮助内容 + 现场图片
 * 快捷操作：找人手 / 找120 / 找校方 / 重新定位
 * 支持游客匿名会话，成功后返回 requestNo / followupCode / closeToken
 */
import { onMounted } from 'vue'
import { useEmergencyHelp } from '@/composables/useEmergencyHelp'

const {
  form,
  isSubmitting,
  publishResult,
  autoLocate,
  quickHelpHand,
  quickCall120,
  quickCallSchool,
  addImage,
  submit,
} = useEmergencyHelp()

onMounted(() => {
  void autoLocate()
})

function handlePrivacyChange(event: Event): void {
  const payload = event as unknown as { detail?: { value?: boolean } }
  form.privacyConsent = Boolean(payload.detail?.value)
}

async function submitHelp(): Promise<void> {
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
  font-size: $font-size-lg;
  color: $text-primary;
  font-weight: $font-weight-semibold;
}

.input-field,
.textarea-field {
  width: 100%;
  background-color: $background;
  border-radius: $radius-md;
  padding: $spacing-sm;
  box-sizing: border-box;
  font-size: $font-size-base;
  color: $text-primary;
}

.textarea-field {
  min-height: 180rpx;
}

.quick-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: $spacing-sm;
}

.quick-btn {
  height: 72rpx;
  border-radius: $radius-md;
  border: 1rpx solid $divider;
  background-color: $background-white;
  color: $text-primary;
  font-size: $font-size-sm;
}

.section-actions {
  margin-top: $spacing-sm;
}

.mini-btn {
  height: 60rpx;
  padding: 0 $spacing-md;
  border-radius: $radius-pill;
  border: 1rpx solid $primary;
  background-color: $background-white;
  color: $primary;
  font-size: $font-size-sm;
}

.privacy-row {
  @include flex-align-center;
  gap: $spacing-sm;
  margin-bottom: $spacing-md;
}

.privacy-text {
  font-size: $font-size-sm;
  color: $text-secondary;
}

.submit-btn {
  @include btn-primary;
  width: 100%;
}

.result-card {
  margin-bottom: calc(#{$spacing-xl} + env(safe-area-inset-bottom));
  padding: $spacing-md;
  border-radius: $radius-lg;
  background-color: $background-white;
  border: 1rpx solid $status-online;
}

.result-title {
  display: block;
  margin-bottom: $spacing-sm;
  color: $status-online;
  font-size: $font-size-lg;
  font-weight: $font-weight-semibold;
}

.result-line {
  display: block;
  color: $text-secondary;
  font-size: $font-size-sm;
  margin-bottom: $spacing-xs;
}
</style>
