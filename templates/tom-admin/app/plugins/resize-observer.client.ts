export default defineNuxtPlugin((nuxtApp) => {
  const isResizeObserverNoise = (error: unknown) => {
    const message = error instanceof Error ? error.message : String(error)
    return message.includes('ResizeObserver')
  }

  nuxtApp.hook('vue:error', (error) => {
    if (import.meta.client && isResizeObserverNoise(error)) {
      clearError()
    }
  })

  nuxtApp.hook('app:error', (error) => {
    if (import.meta.client && isResizeObserverNoise(error)) {
      clearError()
    }
  })

  if (!import.meta.client) {
    return
  }

  window.addEventListener('error', (event) => {
    if (isResizeObserverNoise(event.message)) {
      event.preventDefault()
      event.stopImmediatePropagation()
    }
  }, true)
})
