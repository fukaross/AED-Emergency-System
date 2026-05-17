import { describe, expect, it } from 'vitest'
import {
  buildWeChatDisplayName,
  shouldUseWeChatDisplayName,
} from '@/utils/wechat-display'

describe('wechat display name', () => {
  it('appends masked openid for real wechat users', () => {
    expect(buildWeChatDisplayName('obTw23eNrLnMEI3Bbgk8abcd')).toBe(
      '微信用户 obTw...abcd',
    )
  })

  it('keeps the default name when openid is empty', () => {
    expect(buildWeChatDisplayName('')).toBe('微信用户')
  })

  it('only replaces blank or base wechat names', () => {
    expect(shouldUseWeChatDisplayName('')).toBe(true)
    expect(shouldUseWeChatDisplayName('微信用户')).toBe(true)
    expect(shouldUseWeChatDisplayName('REDACTED')).toBe(false)
  })
})
