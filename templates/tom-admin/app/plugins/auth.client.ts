export default defineNuxtPlugin(async () => {
  const { refreshSession } = useAuthSession()
  await refreshSession()
})
