import { FetchError } from 'ofetch'

export function problemMessage(error: unknown, fallback = 'Request failed') {
  if (error instanceof FetchError) {
    const data = error.data as {
      detail?: string
      title?: string
      errors?: Record<string, string[] | string>
    } | string | null

    if (typeof data === 'string' && data.trim()) {
      return data
    }

    if (data && typeof data === 'object') {
      if (data.errors) {
        const parts = Object.values(data.errors).flat().filter(Boolean)
        if (parts.length) {
          return parts.join(' ')
        }
      }
      return data.detail ?? data.title ?? fallback
    }

    return error.message || fallback
  }

  if (error instanceof Error && error.message) {
    return error.message
  }

  return fallback
}

export function isPasskeyCancel(error: unknown) {
  if (error instanceof DOMException) {
    return error.name === 'NotAllowedError' || error.name === 'AbortError'
  }

  return error instanceof Error && /cancelled or failed/i.test(error.message)
}

export function safeAppRedirect(value: unknown) {
  const path = Array.isArray(value) ? value[0] : value
  if (typeof path === 'string' && path.startsWith('/') && !path.startsWith('//') && !path.includes('\\')) {
    return path
  }
  return '/'
}

export function problemTitle(error: unknown): string {
  if (error instanceof FetchError && error.data && typeof error.data === 'object') {
    const data = error.data as { title?: string, detail?: string }
    return data.title || data.detail || ''
  }
  return ''
}

export function isRequiresTwoFactor(error: unknown) {
  const title = problemTitle(error)
  return title === 'RequiresTwoFactor'
}

export function isLockedOut(error: unknown) {
  const title = problemTitle(error)
  return title === 'LockedOut'
}

export function confirmRedirectStatus(url: string): 'confirmed' | 'failed' | null {
  if (url.includes('status=confirmed')) {
    return 'confirmed'
  }
  if (url.includes('status=failed')) {
    return 'failed'
  }
  return null
}

export function useAuth() {
  const { api, baseURL } = useApi()
  const { refreshSession, signOut, info, loading } = useAuthSession()

  const { clear: clearPendingRegistration } = usePendingRegistration()

  const signOutCookie = async () => {
    await signOut()
    clearPendingRegistration()
  }

  const loginWithPassword = async (
    email: string,
    password: string,
    rememberMe = true,
    extra?: {
      twoFactorCode?: string
      twoFactorRecoveryCode?: string
    }
  ) => {
    const body: Record<string, string> = {
      email,
      password
    }
    if (extra?.twoFactorCode) {
      body.twoFactorCode = extra.twoFactorCode
    }
    if (extra?.twoFactorRecoveryCode) {
      body.twoFactorRecoveryCode = extra.twoFactorRecoveryCode
    }
    await api('/identity/login', {
      method: 'POST',
      query: rememberMe ? { useSessionCookies: false } : { useSessionCookies: true },
      body,
      skipCsrf: true
    })
    return refreshSession()
  }

  const loginFailureMessage = (error: unknown) => {
    if (error instanceof FetchError && error.statusCode === 401) {
      return 'Invalid credentials.'
    }
    return problemMessage(error, 'Invalid credentials.')
  }

  const confirmEmail = async (query: {
    userId: string
    code: string
    changedEmail?: string
  }) => {
    const flow = query.changedEmail ? 'change-email' : 'confirm'
    const path = '/identity/confirmEmail'
    const url = `${baseURL.value}${path}`

    try {
      // Browser fetch cannot follow the API 302 onto this Nuxt origin (CORS).
      const result = await $fetch<{
        status: 'confirmed' | 'failed'
        flow: 'confirm' | 'change-email'
        location?: string
      }>('/api/confirm-email', {
        query: {
          userId: query.userId,
          code: query.code,
          changedEmail: query.changedEmail || undefined
        }
      })

      return { status: result.status, flow: result.flow }
    } catch (error) {
      const failedUrl = error && typeof error === 'object' && 'response' in error
        ? String((error as { response?: { url?: string } }).response?.url ?? '')
        : ''
      const status = confirmRedirectStatus(failedUrl) ?? 'failed'
      return { status, flow, error: problemMessage(error), url: failedUrl || url }
    }
  }

  return {
    api,
    info,
    loading,
    refreshCookieSession: refreshSession,
    signOutCookie,
    loginWithPassword,
    loginFailureMessage,
    confirmEmail,
    problemMessage,
    isPasskeyCancel,
    isRequiresTwoFactor,
    isLockedOut,
    safeAppRedirect
  }
}
