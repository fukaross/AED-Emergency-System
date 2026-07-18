import { ref } from 'vue'

const DEFAULT_MAP_HEIGHT_VH = 45
const MIN_MAP_HEIGHT_VH = 25
const MAX_MAP_HEIGHT_VH = 70

/** Encapsulates drag gesture state and keeps the rescue map within usable panel bounds. */
export function useSessionMapResize() {
  const mapHeightVh = ref(DEFAULT_MAP_HEIGHT_VH)
  let dragStartY = 0
  let dragStartHeight = DEFAULT_MAP_HEIGHT_VH
  let windowHeight = 0

  function onDragStart(event: TouchEvent): void {
    dragStartY = event.touches[0].clientY
    dragStartHeight = mapHeightVh.value
    windowHeight = uni.getWindowInfo().windowHeight
  }

  function onDragMove(event: TouchEvent): void {
    if (!windowHeight) return
    const deltaVh = ((event.touches[0].clientY - dragStartY) / windowHeight) * 100
    mapHeightVh.value = Math.min(
      MAX_MAP_HEIGHT_VH,
      Math.max(MIN_MAP_HEIGHT_VH, dragStartHeight + deltaVh),
    )
  }

  return { mapHeightVh, onDragStart, onDragMove }
}
