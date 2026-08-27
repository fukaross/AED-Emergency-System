/** 会话内临时颜色分配：同一场会话内同一 openId 颜色稳定，不同会话使用不同随机种子重新分配。 */
export interface ParticipantColor {
  readonly background: string
  readonly accent: string
}

export function createSessionColorSeed(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`
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
