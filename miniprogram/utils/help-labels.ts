import type { HelpStatus } from '@/types/help'

const HELP_STATUS_TEXT: Readonly<Record<HelpStatus, string>> = {
  published: '已发布',
  responding: '前往中',
  pending_review: '待审核',
  closed: '已完成',
  false_alarm: '误报',
}

const HELP_ACTION_TEXT: Readonly<Record<string, string>> = {
  published: '已发布',
  responded: '已响应',
  pending_review: '报告待审核',
  closed: '已关闭',
  false_alarm: '误报关闭',
}

export function getHelpStatusText(status: HelpStatus): string {
  return HELP_STATUS_TEXT[status]
}

export function getHelpStatusClass(status: HelpStatus | undefined): string {
  return `status-tag--${status ?? 'unknown'}`
}

export function getHelpActionText(action: string): string {
  return HELP_ACTION_TEXT[action] ?? action
}
