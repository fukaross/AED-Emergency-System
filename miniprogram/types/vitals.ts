/**
 * 生命体征相关类型定义
 *
 * 来源：CLAUDE.md §4.1.4 + BackEnd.md §4.7A
 */

export type VitalSource = 'mock' | 'device' | 'manual'

export type HeartRateSignalQuality = 'good' | 'weak'

export type HeartRateStatus =
  | 'normal'
  | 'fast'
  | 'slow'
  | 'abnormal_fast'
  | 'abnormal_slow'
  | 'high_risk_fast'
  | 'high_risk_slow'
  | 'detecting'
  | 'disconnected'
  | 'no_data'
  | 'weak_signal'

export type VitalAlertState =
  | 'normal'
  | 'suspected_abnormal'
  | 'alert_sent'
  | 'recovered'

export interface HeartRateReading {
  readonly id: string
  readonly bpm: number
  readonly measuredAt: string
  readonly source: VitalSource
  readonly signalQuality?: HeartRateSignalQuality
}

export interface BloodPressureReading {
  readonly id: string
  readonly measuredAt: string
  readonly systolic: number
  readonly diastolic: number
  readonly source: VitalSource
}

export interface VitalSignsCurrent {
  readonly heartRate: HeartRateReading | null
  readonly heartRateStatus: HeartRateStatus
  readonly latestBloodPressure: BloodPressureReading | null
  readonly bloodPressureStale: boolean
  readonly alertState: VitalAlertState
}

export interface HeartRateTrendPoint {
  readonly windowStart: string
  readonly windowEnd: string
  readonly averageBpm: number
  readonly minBpm?: number | null
  readonly maxBpm?: number | null
  readonly sampleCount: number
}

export interface HeartRateTrendList {
  readonly items: readonly HeartRateTrendPoint[]
}

export interface HeartRateTrendParams {
  readonly from?: string
  readonly to?: string
  readonly intervalMinutes?: number
}

export interface CreateVitalAlertPayload {
  readonly heartRateBpm: number
  readonly measuredAt: string
  readonly latitude?: number
  readonly longitude?: number
  readonly address?: string
}

export interface VitalAlertResult {
  readonly alertId?: string | null
  readonly alertState: VitalAlertState
  readonly notificationTriggered: boolean
  readonly recipientsCount: number
  readonly cooldownUntil?: string | null
}
