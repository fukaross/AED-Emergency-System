// 生命体征 —— 心率、血压、缓存和告警状态机
import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  createVitalAlert,
  fetchCurrentVitalSigns,
  fetchHeartRateTrends,
} from '@/api/vitals'
import type {
  BloodPressureReading,
  HeartRateReading,
  HeartRateStatus,
  HeartRateTrendPoint,
  VitalAlertState,
} from '@/types/vitals'
import {
  VITAL_ABNORMAL_CONFIRM_SAMPLES,
  VITAL_ALERT_COOLDOWN_MS,
  VITAL_HEART_RATE_REFRESH_MS,
  VITAL_HIGH_RISK_CONFIRM_SAMPLES,
  VITAL_RECOVERY_STABLE_MS,
  VITAL_SHORT_TERM_CACHE_MS,
  VITAL_TREND_WINDOW_MINUTES,
} from '@/utils/constants'

export const useVitalsStore = defineStore('vitals', () => {
  const currentHeartRate = ref<HeartRateReading | null>(null)
  const latestBloodPressure = ref<BloodPressureReading | null>(null)
  const heartRateStatus = ref<HeartRateStatus>('detecting')
  const alertState = ref<VitalAlertState>('normal')
  const shortTermHeartRates = ref<HeartRateReading[]>([])
  const trendPoints24h = ref<readonly HeartRateTrendPoint[]>([])
  const bloodPressureStale = ref(false)
  const loading = ref(false)
  const monitoring = ref(false)

  let refreshTimer: ReturnType<typeof setInterval> | null = null
  let lastCandidateStatus: HeartRateStatus | null = null
  let consecutiveCandidateCount = 0
  let cooldownUntil = 0
  let normalSince = 0

  async function refreshCurrent(): Promise<void> {
    loading.value = true
    try {
      const current = await fetchCurrentVitalSigns()
      currentHeartRate.value = current.heartRate
      latestBloodPressure.value = current.latestBloodPressure
      heartRateStatus.value = current.heartRateStatus
      bloodPressureStale.value = current.bloodPressureStale
      cacheHeartRate(current.heartRate)
      await updateAlertState(current.heartRate, current.heartRateStatus)
    } finally {
      loading.value = false
    }
  }

  async function loadTrends(): Promise<void> {
    const result = await fetchHeartRateTrends({
      intervalMinutes: VITAL_TREND_WINDOW_MINUTES,
    })
    trendPoints24h.value = result.items
  }

  function startMonitoring(): void {
    if (refreshTimer !== null) return
    monitoring.value = true
    void refreshCurrent()
    void loadTrends()
    refreshTimer = setInterval(() => {
      void refreshCurrent()
    }, VITAL_HEART_RATE_REFRESH_MS)
  }

  function stopMonitoring(): void {
    if (refreshTimer !== null) {
      clearInterval(refreshTimer)
      refreshTimer = null
    }
    monitoring.value = false
  }

  function cacheHeartRate(reading: HeartRateReading | null): void {
    if (!reading) return
    const expiresAt = Date.now() - VITAL_SHORT_TERM_CACHE_MS
    shortTermHeartRates.value = [...shortTermHeartRates.value, reading]
      .filter((item) => Date.parse(item.measuredAt) >= expiresAt)
  }

  async function updateAlertState(
    reading: HeartRateReading | null,
    status: HeartRateStatus,
  ): Promise<void> {
    if (!reading || !isAlertCandidate(status)) {
      handleNonCandidateStatus(status)
      return
    }

    if (Date.now() < cooldownUntil) return
    const count = updateConsecutiveCandidate(status)
    if (count < getRequiredSamples(status)) return
    await reportAlertCandidate(reading)
  }

  function handleNonCandidateStatus(status: HeartRateStatus): void {
    lastCandidateStatus = null
    consecutiveCandidateCount = 0
    if (status === 'normal') {
      applyRecoveryState()
      return
    }
    normalSince = 0
  }

  function applyRecoveryState(): void {
    if (alertState.value === 'normal') return
    if (normalSince === 0) {
      normalSince = Date.now()
      return
    }
    if (Date.now() - normalSince >= VITAL_RECOVERY_STABLE_MS) {
      alertState.value = 'recovered'
    }
  }

  function updateConsecutiveCandidate(status: HeartRateStatus): number {
    if (lastCandidateStatus !== status) {
      lastCandidateStatus = status
      consecutiveCandidateCount = 1
      return consecutiveCandidateCount
    }
    consecutiveCandidateCount += 1
    return consecutiveCandidateCount
  }

  async function reportAlertCandidate(reading: HeartRateReading): Promise<void> {
    cooldownUntil = Date.now() + VITAL_ALERT_COOLDOWN_MS
    const result = await createVitalAlert({
      heartRateBpm: reading.bpm,
      measuredAt: reading.measuredAt,
    })
    alertState.value = result.alertState
  }

  return {
    currentHeartRate,
    latestBloodPressure,
    heartRateStatus,
    alertState,
    shortTermHeartRates,
    trendPoints24h,
    bloodPressureStale,
    loading,
    monitoring,
    refreshCurrent,
    loadTrends,
    startMonitoring,
    stopMonitoring,
  }
})

function isAlertCandidate(status: HeartRateStatus): boolean {
  return [
    'abnormal_fast',
    'abnormal_slow',
    'high_risk_fast',
    'high_risk_slow',
  ].includes(status)
}

function getRequiredSamples(status: HeartRateStatus): number {
  if (status === 'high_risk_fast' || status === 'high_risk_slow') {
    return VITAL_HIGH_RISK_CONFIRM_SAMPLES
  }
  return VITAL_ABNORMAL_CONFIRM_SAMPLES
}
