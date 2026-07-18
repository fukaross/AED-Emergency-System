import { computed, ref } from 'vue'
import type { RescueMineItem } from '@/types/rescue-session'

export interface RescueSessionDiscoveryAdapter {
  fetchMine(options: { readonly silent?: boolean }): Promise<RescueMineItem[]>
}

/**
 * Owns account-bound session discovery and rejects responses from an old owner or request.
 */
export class RescueSessionDiscovery {
  readonly items = ref<RescueMineItem[]>([])
  readonly loaded = ref(false)
  readonly totalUnread = computed(() =>
    this.items.value.reduce((sum, item) => sum + item.unreadCount, 0))
  readonly hasActiveSession = computed(() => this.items.value.length > 0)

  private ownerId = ''
  private generation = 0
  private latestRequest = 0

  constructor(private readonly adapter: RescueSessionDiscoveryAdapter) {}

  async load(ownerId: string, silent = false): Promise<void> {
    if (!ownerId) {
      this.reset()
      return
    }
    this.bindOwner(ownerId)
    const generation = this.generation
    const request = ++this.latestRequest
    const items = await this.adapter.fetchMine({ silent })
    if (!this.owns(ownerId, generation, request)) return
    this.items.value = items
    this.loaded.value = true
  }

  reset(): void {
    this.ownerId = ''
    this.invalidateRequests()
    this.clearState()
  }

  private bindOwner(ownerId: string): void {
    if (this.ownerId === ownerId) return
    this.ownerId = ownerId
    this.invalidateRequests()
    this.clearState()
  }

  private invalidateRequests(): void {
    this.generation += 1
    this.latestRequest += 1
  }

  private clearState(): void {
    this.items.value = []
    this.loaded.value = false
  }

  private owns(ownerId: string, generation: number, request: number): boolean {
    return this.ownerId === ownerId
      && this.generation === generation
      && this.latestRequest === request
  }
}
