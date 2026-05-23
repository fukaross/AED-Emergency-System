export interface RequestTicket {
  readonly id: number
}

export interface RequestSequencer {
  next(): RequestTicket
  isCurrent(ticket: RequestTicket): boolean
}

export function createRequestSequencer(): RequestSequencer {
  let currentId = 0

  return {
    next() {
      currentId += 1
      return { id: currentId }
    },
    isCurrent(ticket) {
      return ticket.id === currentId
    },
  }
}
