export interface RequestTicket {
  readonly id: number
}

export interface RequestSequencer {
  next(): RequestTicket
  invalidate(): void
  isCurrent(ticket: RequestTicket): boolean
}

export function createRequestSequencer(): RequestSequencer {
  let currentId = 0

  return {
    next() {
      currentId += 1
      return { id: currentId }
    },
    invalidate() {
      currentId += 1
    },
    isCurrent(ticket) {
      return ticket.id === currentId
    },
  }
}
