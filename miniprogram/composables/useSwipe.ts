import { ref } from 'vue'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function useSwipe(onSwipeLeft: () => void, onSwipeRight: () => void) {
  const transitionClass = ref('slide-in-right')
  let startX = 0
  let startY = 0

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  function onTouchStart(e: any) {
    const t = e.touches?.[0]
    if (!t) return
    startX = t.pageX
    startY = t.pageY
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  function onTouchEnd(e: any) {
    const t = e.changedTouches?.[0]
    if (!t) return
    const dx = t.pageX - startX
    const dy = t.pageY - startY
    if (Math.abs(dx) < 60 || Math.abs(dy) > Math.abs(dx)) return

    if (dx < 0) {
      transitionClass.value = 'slide-in-right'
      onSwipeLeft()
    } else if (dx > 0) {
      transitionClass.value = 'slide-in-left'
      onSwipeRight()
    }
  }

  return { transitionClass, onTouchStart, onTouchEnd }
}
