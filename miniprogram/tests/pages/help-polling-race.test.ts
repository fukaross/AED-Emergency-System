import { ref } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { HelpRequestDetail } from '@/types/help'
import type { RescueView } from '@/types/rescue-session'

const apiMocks = vi.hoisted(() => ({
  fetchHelpDetail: vi.fn(),
  fetchRescueView: vi.fn(),
}))

vi.mock('@/api/help', () => ({ fetchHelpDetail: apiMocks.fetchHelpDetail }))
vi.mock('@/api/rescue-session', () => ({ fetchRescueView: apiMocks.fetchRescueView }))

import { useHelpDetailPolling } from '@/pages-sub/help/useHelpDetailPolling'
import { useHelpRescueView } from '@/pages-sub/help/useHelpRescueView'

interface Deferred<T> {
  readonly promise: Promise<T>
  readonly resolve: (value: T) => void
}

function deferred<T>(): Deferred<T> {
  let resolve = (_value: T) => undefined
  const promise = new Promise<T>((done) => { resolve = done })
  return { promise, resolve }
}

function buildDetail(id: string): HelpRequestDetail {
  return {
    id,
    status: 'responding',
    canSubmitReport: false,
  } as HelpRequestDetail
}

function buildView(id: string): RescueView {
  return {
    helpId: id,
    helpStatus: 'responding',
    participants: [],
  } as unknown as RescueView
}

describe('help polling request ownership', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    apiMocks.fetchHelpDetail.mockReset()
    apiMocks.fetchRescueView.mockReset()
  })

  it('does not apply an old detail response after stop and help-id switch', async () => {
    const first = deferred<HelpRequestDetail>()
    const second = deferred<HelpRequestDetail>()
    apiMocks.fetchHelpDetail
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)
    const detail = ref<HelpRequestDetail | null>(buildDetail('help-1'))
    const applied = vi.fn()
    const polling = useHelpDetailPolling({
      detail,
      applyDetail: applied,
      handleAccessEnded: vi.fn(),
    })

    const oldRequest = polling.refreshHelpDetail()
    polling.stopHelpPolling()
    detail.value = buildDetail('help-2')
    const currentRequest = polling.refreshHelpDetail()
    first.resolve(buildDetail('help-1'))
    second.resolve(buildDetail('help-2'))
    await Promise.all([oldRequest, currentRequest])

    expect(applied).toHaveBeenCalledTimes(1)
    expect(applied).toHaveBeenCalledWith(expect.objectContaining({ id: 'help-2' }))
  })

  it('keeps the new rescue request busy when an old generation finishes', async () => {
    const first = deferred<RescueView>()
    const second = deferred<RescueView>()
    apiMocks.fetchRescueView
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)
    const detail = ref<HelpRequestDetail | null>(buildDetail('help-1'))
    const rescue = useHelpRescueView(detail)

    const oldRequest = rescue.refreshRescueView()
    rescue.suspendRescueView()
    const currentRequest = rescue.refreshRescueView()
    first.resolve(buildView('help-1'))
    await oldRequest
    await rescue.refreshRescueView()

    expect(apiMocks.fetchRescueView).toHaveBeenCalledTimes(2)
    second.resolve(buildView('help-1'))
    await currentRequest
    expect(rescue.rescueView.value?.helpId).toBe('help-1')
  })

  it('surfaces detail access revocation and logs other failures without payloads', async () => {
    const accessEnded = vi.fn()
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const detail = ref<HelpRequestDetail | null>(buildDetail('help-1'))
    const polling = useHelpDetailPolling({
      detail,
      applyDetail: vi.fn(),
      handleAccessEnded: accessEnded,
    })
    apiMocks.fetchHelpDetail.mockRejectedValueOnce(
      Object.assign(new Error('forbidden body'), { statusCode: 403, code: 1003 }),
    )
    await polling.refreshHelpDetail()

    expect(accessEnded).toHaveBeenCalledTimes(1)
    expect(warn).not.toHaveBeenCalled()

    apiMocks.fetchHelpDetail.mockRejectedValueOnce(new Error('private network detail'))
    await polling.refreshHelpDetail()
    expect(warn).toHaveBeenCalledWith(
      '[HelpDetailPolling] GET /api/help/{id} status=network code=none',
    )
    expect(JSON.stringify(warn.mock.calls)).not.toContain('private network detail')
  })

  it('logs rescue-view network failures without response bodies', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    apiMocks.fetchRescueView.mockRejectedValueOnce(new Error('coordinates in body'))
    const rescue = useHelpRescueView(ref<HelpRequestDetail | null>(buildDetail('help-1')))

    await rescue.refreshRescueView()

    expect(warn).toHaveBeenCalledWith(
      '[RescueViewPolling] GET /api/help/{id}/rescue-view status=network code=none',
    )
    expect(JSON.stringify(warn.mock.calls)).not.toContain('coordinates in body')
  })
})
