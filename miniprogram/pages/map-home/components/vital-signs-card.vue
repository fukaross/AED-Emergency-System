<template>
  <view class="vital-card" :class="cardClass" :style="cardStyle">
    <view class="vital-card__title-row">
      <image
        v-if="showActiveHeart"
        class="vital-card__heart-image"
        :src="RED_HEART_ICON"
        mode="aspectFit"
      />
      <text v-else class="vital-card__heart-text">{{ heartIcon }}</text>
      <text class="vital-card__title">{{ titleText }}</text>
      <text v-if="showWarningIcon" class="vital-card__warning">!</text>
    </view>

    <view class="vital-card__main">
      <text class="vital-card__value">{{ mainValue }}</text>
      <text v-if="mainUnit" class="vital-card__unit">{{ mainUnit }}</text>
    </view>

    <view class="vital-card__blood">
      <text>{{ bloodPressureText }}</text>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useNavbarSafeArea } from '@/composables/useNavbarSafeArea'
import type {
  BloodPressureReading,
  HeartRateReading,
  HeartRateStatus,
} from '@/types/vitals'

const HELP_TOP_GAP_PX = 10
const CARD_TOP_OFFSET_RPX = 136
const RED_HEART_ICON = '/static/icons/red-heart.svg'

const props = defineProps<{
  heartRate: HeartRateReading | null
  latestBloodPressure: BloodPressureReading | null
  heartRateStatus: HeartRateStatus
  bloodPressureStale: boolean
}>()

const { navbarBottom } = useNavbarSafeArea()

const cardStyle = computed(() => ({
  top: `${navbarBottom.value + HELP_TOP_GAP_PX + uni.upx2px(CARD_TOP_OFFSET_RPX)}px`,
}))

const titleText = computed(() => {
  const titleMap: Partial<Record<HeartRateStatus, string>> = {
    fast: '心率偏快',
    slow: '心率偏慢',
    abnormal_fast: '心率异常',
    abnormal_slow: '心率偏低',
    high_risk_fast: '心率异常',
    high_risk_slow: '心率过低',
  }
  return titleMap[props.heartRateStatus] ?? '当前心率'
})

const mainValue = computed(() => {
  if (props.heartRateStatus === 'detecting') return '检测中'
  if (props.heartRateStatus === 'disconnected') return '未连接'
  if (props.heartRateStatus === 'weak_signal') return '信号弱'
  if (props.heartRateStatus === 'no_data' || !props.heartRate) return '--'
  return String(props.heartRate.bpm)
})

const mainUnit = computed(() => {
  if (props.heartRateStatus === 'no_data') return 'bpm'
  return props.heartRate && isNumericStatus(props.heartRateStatus) ? 'bpm' : ''
})

const bloodPressureText = computed(() => {
  if (props.bloodPressureStale) return '血压未更新'
  if (props.heartRateStatus === 'disconnected' || !props.latestBloodPressure) {
    return '最近血压 --'
  }

  const { systolic, diastolic } = props.latestBloodPressure
  return `最近血压 ${systolic}/${diastolic}`
})

const isMuted = computed(() =>
  ['disconnected', 'no_data', 'weak_signal'].includes(props.heartRateStatus),
)

const isRisk = computed(() =>
  ['abnormal_fast', 'abnormal_slow'].includes(props.heartRateStatus),
)

const isHighRisk = computed(() =>
  ['high_risk_fast', 'high_risk_slow'].includes(props.heartRateStatus),
)

const cardClass = computed(() => ({
  'is-muted': isMuted.value,
  'is-risk': isRisk.value,
  'is-high-risk': isHighRisk.value,
}))

const heartIcon = computed(() => (isMuted.value ? '♡' : '♥'))
const showActiveHeart = computed(() => !isMuted.value)
const showWarningIcon = computed(() => isHighRisk.value)

function isNumericStatus(status: HeartRateStatus): boolean {
  return !['detecting', 'disconnected', 'weak_signal'].includes(status)
}
</script>

<style lang="scss" scoped>
.vital-card {
  position: fixed;
  right: $spacing-md;
  z-index: 100;
  width: 196rpx;
  min-height: 112rpx;
  box-sizing: border-box;
  padding: 14rpx 18rpx;
  border: 1rpx solid rgba(0, 0, 0, 0.06);
  border-radius: 22rpx;
  background-color: rgba(255, 255, 255, 0.94);
  box-shadow: 0 6rpx 20rpx rgba(0, 0, 0, 0.06);

  &.is-risk {
    border-color: rgba(229, 57, 53, 0.42);
  }

  &.is-high-risk {
    border-color: $primary;
  }

  &.is-muted {
    .vital-card__heart-text,
    .vital-card__value {
      color: $text-tertiary-a11y;
    }
  }

  &__title-row {
    display: flex;
    align-items: center;
    min-width: 0;
  }

  &__heart-image {
    flex-shrink: 0;
    width: 24rpx;
    height: 24rpx;
    margin-right: 8rpx;
    display: block;
  }

  &__heart-text {
    flex-shrink: 0;
    width: 24rpx;
    margin-right: 8rpx;
    font-size: 24rpx;
    line-height: 1;
    color: $primary;
    text-align: center;
  }

  &__title {
    min-width: 0;
    font-size: 22rpx;
    line-height: $line-height-tight;
    color: $text-primary;
    font-weight: $font-weight-medium;
  }

  &__warning {
    flex-shrink: 0;
    width: 20rpx;
    height: 20rpx;
    margin-left: 6rpx;
    border-radius: $radius-full;
    background-color: $primary;
    color: $text-white;
    font-size: 16rpx;
    line-height: 20rpx;
    text-align: center;
    font-weight: $font-weight-bold;
  }

  &__main {
    display: flex;
    align-items: baseline;
    margin-top: 8rpx;
  }

  &__value {
    font-size: 38rpx;
    line-height: 1;
    color: $primary;
    font-weight: $font-weight-bold;
  }

  &__unit {
    margin-left: 6rpx;
    font-size: 20rpx;
    color: $text-secondary;
  }

  &__blood {
    margin-top: 10rpx;
    font-size: 20rpx;
    line-height: $line-height-tight;
    color: $text-secondary;
    @include text-ellipsis;
  }
}
</style>
