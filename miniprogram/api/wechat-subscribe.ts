// 微信订阅消息 —— 请求授权、保存授权结果（仅真实微信档；演示档不发送）
import { post } from '@/services/request'
import { WECHAT_ALERT_TEMPLATE_ID } from '@/utils/constants'
import { USE_REAL_WECHAT_FLOW } from '@/utils/runtime-env'

type SubscribeStatus = 'accept' | 'acceptWithAudio' | 'reject' | 'ban' | 'filter'

interface SaveSubscribeDecisionPayload {
  readonly templateId: string
  readonly status: SubscribeStatus
}

interface SubscribeMessageApi {
  requestSubscribeMessage(options: {
    tmplIds: string[]
    success: (result: Record<string, SubscribeStatus | string>) => void
    fail: (error: { errMsg?: string }) => void
  }): void
}

export async function requestAlertSubscribe(): Promise<SubscribeStatus> {
  if (!USE_REAL_WECHAT_FLOW) {
    throw new Error('演示模式不发送真实订阅通知')
  }
  if (!WECHAT_ALERT_TEMPLATE_ID) {
    throw new Error('订阅模板未配置')
  }

  const status = await requestSubscribe(WECHAT_ALERT_TEMPLATE_ID)
  await saveSubscribeDecision({
    templateId: WECHAT_ALERT_TEMPLATE_ID,
    status,
  })

  return status
}

function requestSubscribe(templateId: string): Promise<SubscribeStatus> {
  const api = uni as unknown as SubscribeMessageApi

  return new Promise((resolve, reject) => {
    api.requestSubscribeMessage({
      tmplIds: [templateId],
      success: (result) => {
        resolve(normalizeStatus(result[templateId]))
      },
      fail: (error) => {
        reject(new Error(error.errMsg || '订阅授权失败'))
      },
    })
  })
}

function saveSubscribeDecision(
  payload: SaveSubscribeDecisionPayload,
): Promise<{ templateId: string; status: SubscribeStatus }> {
  return post<{ templateId: string; status: SubscribeStatus }>(
    '/api/wechat/subscriptions',
    payload as unknown as Record<string, unknown>,
  )
}

function normalizeStatus(status: string | undefined): SubscribeStatus {
  if (
    status === 'accept' ||
    status === 'acceptWithAudio' ||
    status === 'reject' ||
    status === 'ban' ||
    status === 'filter'
  ) {
    return status
  }

  return 'reject'
}
