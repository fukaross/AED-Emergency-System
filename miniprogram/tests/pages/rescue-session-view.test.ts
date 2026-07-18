import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const sessionPage = readFileSync(
  new URL('../../pages-sub/rescue/session.vue', import.meta.url),
  'utf8',
)

describe('rescue session page state', () => {
  it('renders a visible state while the initial join request is pending', () => {
    expect(sessionPage).toContain('v-else-if="joining"')
    expect(sessionPage).toContain('正在加入会话')
  })

  it('terminates page runtime when global authentication ends', () => {
    expect(sessionPage).toContain('watch(isLoggedIn')
    expect(sessionPage).toContain("terminateSessionAccess(helpId.value, '登录状态已失效，请重新登录')")
    expect(sessionPage).toContain('stopSessionRuntime()')
    expect(sessionPage).toContain('sessionStore.resetSession(id)')
  })
})
