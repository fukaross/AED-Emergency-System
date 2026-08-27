/** 会话页「结果汇报」入口可见性：仅进行中的 published/responding 且会话未关闭时可跳转提交报告。 */
const REPORTABLE_HELP_STATUSES: ReadonlySet<string> = new Set([
  'published',
  'responding',
])

export function canShowResultReport(
  helpStatus: string | undefined,
  sessionClosed: boolean,
): boolean {
  return !sessionClosed
    && helpStatus !== undefined
    && REPORTABLE_HELP_STATUSES.has(helpStatus)
}
