import type { RescueMessage, RescueSessionSnapshot } from '@/types/rescue-session'

export interface Deferred<T> {
  readonly promise: Promise<T>
  readonly resolve: (value: T) => void
}

export function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((done) => { resolve = done })
  return { promise, resolve }
}

export function buildMessage(seq: number): RescueMessage {
  return {
    id: `message-${seq}`,
    seq,
    kind: 'text',
    senderOpenId: 'rescuer-a',
    senderName: '急救员 A',
    text: `消息 ${seq}`,
    eventType: null,
    action: null,
    createdAt: '2026-07-17T10:00:00.000Z',
  }
}

export function buildSnapshot(helpId: string): RescueSessionSnapshot {
  return {
    helpId,
    requestNo: helpId,
    helpStatus: 'responding',
    sos: { lat: 19.97, lng: 110.51, address: '测试点' },
    me: { openId: 'rescuer-a', takenAed: false, arrived: false, lastReadSeq: 0 },
    participants: [{
      openId: 'rescuer-a',
      displayName: '急救员 A',
      joinedAt: '2026-07-17T09:59:00.000Z',
      takenAed: false,
      arrived: false,
      signal: 'ok',
      location: {
        lat: 19.971,
        lng: 110.511,
        reportedAt: '2026-07-17T10:00:00.000Z',
      },
    }],
    messages: [buildMessage(1)],
    latestSeq: 1,
    serverTime: '2026-07-17T10:00:01.000Z',
    timing: {
      signalLostAfterSeconds: 45,
      locationGoneAfterSeconds: 90,
      locationReportIntervalSeconds: 5,
    },
  }
}
