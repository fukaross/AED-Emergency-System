const DEFAULT_WECHAT_NAME = '微信用户'
const OPEN_ID_VISIBLE_CHARS = 4

export function buildWeChatDisplayName(openId: string): string {
  const maskedOpenId = maskOpenId(openId)
  return maskedOpenId ? `${DEFAULT_WECHAT_NAME} ${maskedOpenId}` : DEFAULT_WECHAT_NAME
}

export function shouldUseWeChatDisplayName(name: string): boolean {
  return name.trim() === '' || name === DEFAULT_WECHAT_NAME
}

function maskOpenId(openId: string): string {
  const value = openId.trim()
  if (value.length <= OPEN_ID_VISIBLE_CHARS * 2) return value

  const prefix = value.slice(0, OPEN_ID_VISIBLE_CHARS)
  const suffix = value.slice(-OPEN_ID_VISIBLE_CHARS)
  return `${prefix}...${suffix}`
}
