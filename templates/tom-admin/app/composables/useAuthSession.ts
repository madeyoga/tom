import type { ManageInfo } from '~/types/auth'

export function useAuthSession() {
  const { email } = useAuthState()
  const { api, clearCsrf } = useApi()
  const { clear: clearPendingRegistration } = usePendingRegistration()
  const loading = useState('auth-session-loading', () => false)
  const info = useState<ManageInfo | null>('auth-manage-info', () => null)

  const refreshSession = async () => {
    loading.value = true
    try {
      const manage = await api<ManageInfo>('/identity/manage/info')
      info.value = manage
      email.value = manage.email ?? null
      return manage
    } catch {
      info.value = null
      email.value = null
      return null
    } finally {
      loading.value = false
    }
  }

  const signOut = async () => {
    try {
      await api('/identity/logout', {
        method: 'POST'
      })
    } catch {
      // Local cleanup still runs.
    } finally {
      info.value = null
      email.value = null
      clearCsrf()
      clearPendingRegistration()
    }
  }

  return {
    loading,
    info,
    refreshSession,
    signOut
  }
}
