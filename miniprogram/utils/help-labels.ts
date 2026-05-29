// 求助状态、类型的显示文案
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

const HELP_USER_ROLE_TEXT: Readonly<Record<string, string>> = {
  requester: '我发布的',
  reporter: '我参与的',
}

const HELP_USER_ROLE_CLASS: Readonly<Record<string, string>> = {
  requester: 'role-tag--requester',
  reporter: 'role-tag--reporter',
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

export function getHelpUserRoleText(role: string): string {
  return HELP_USER_ROLE_TEXT[role] ?? role
}

export function getHelpUserRoleClass(role: string): string {
  return HELP_USER_ROLE_CLASS[role] ?? ''
}
