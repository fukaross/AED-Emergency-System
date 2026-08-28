/** 会话内临时颜色分配：同一场会话内同一 openId 颜色稳定，不同会话使用 helpId 派生不同种子。 */
export interface ParticipantColor {
  readonly background: string
  readonly accent: string
}

export function createSessionColorSeed(helpId: string): string {
  let hash = 5381
  for (let i = 0; i < helpId.length; i += 1) {
    hash = ((hash << 5) + hash) ^ helpId.charCodeAt(i)
  }
  return `help-${(hash >>> 0).toString(36)}`
}

function hashString(input: string): number {
  let hash = 5381
  for (let i = 0; i < input.length; i += 1) {
    hash = ((hash << 5) + hash) ^ input.charCodeAt(i)
  }
  return hash >>> 0
}

export function participantColorFor(
  seed: string,
  openId: string,
): ParticipantColor {
  const hue = hashString(`${seed}:${openId}`) % 360
  return {
    background: `hsl(${hue}, 60%, 90%)`,
    accent: `hsl(${hue}, 70%, 42%)`,
  }
}

export function buildParticipantColorMap(
  participants: readonly { openId: string }[],
  seed: string,
): ReadonlyMap<string, ParticipantColor> {
  const map = new Map<string, ParticipantColor>()
  for (const participant of participants) {
    map.set(participant.openId, participantColorFor(seed, participant.openId))
  }
  return map
}
