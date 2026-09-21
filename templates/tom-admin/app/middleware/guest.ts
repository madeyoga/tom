export default defineNuxtRouteMiddleware(async () => {
  if (import.meta.server) {
    return
  }

  const { refreshCookieSession, info } = useAuth()
  await refreshCookieSession()

  if (info.value) {
    return navigateTo('/')
  }
})
