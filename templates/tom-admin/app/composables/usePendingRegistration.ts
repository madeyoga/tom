export const EMAIL_CONFIRMED_CHANNEL = 'tom-admin-email-confirmed'
export const PENDING_REGISTRATION_STATE_KEY = 'app-pending-registration'
export const PENDING_REGISTRATION_TTL_MS = 8 * 60 * 1000

export type PendingRegistrationMethod = 'password' | 'passkey'

export type PendingRegistration = {
  email: string
  method: PendingRegistrationMethod
  createdAt: number
  password?: string
}

export type EmailConfirmedMessage = {
  confirmed: true
  email?: string
}

let expireTimer: ReturnType<typeof setTimeout> | null = null

function emailsEqual(a: string, b: string) {
  return a.trim().toLowerCase() === b.trim().toLowerCase()
}

export function isEmailConfirmedMessage(data: unknown): data is EmailConfirmedMessage {
  return !!data
    && typeof data === 'object'
    && (data as { confirmed?: unknown }).confirmed === true
}

export function pendingEmailMatches(stashEmail: string, other?: string | null) {
  if (!other) {
    return true
  }
  return emailsEqual(stashEmail, other)
}

function clearExpireTimer() {
  if (expireTimer) {
    clearTimeout(expireTimer)
    expireTimer = null
  }
}

export function usePendingRegistration() {
  const pending = useState<PendingRegistration | null>(PENDING_REGISTRATION_STATE_KEY, () => null)

  if (import.meta.server) {
    pending.value = null
  }

  const clear = () => {
    pending.value = null
    clearExpireTimer()
  }

  const scheduleExpiry = (createdAt: number) => {
    if (!import.meta.client) {
      return
    }
    clearExpireTimer()
    const remaining = createdAt + PENDING_REGISTRATION_TTL_MS - Date.now()
    if (remaining <= 0) {
      pending.value = null
      return
    }
    expireTimer = setTimeout(() => {
      expireTimer = null
      pending.value = null
    }, remaining)
  }

  const syncExpiry = () => {
    if (!pending.value) {
      clearExpireTimer()
      return
    }
    scheduleExpiry(pending.value.createdAt)
  }

  const stash = (input: {
    email: string
    method: PendingRegistrationMethod
    password?: string
  }) => {
    if (!import.meta.client) {
      return
    }

    const email = input.email.trim()
    pending.value = {
      email,
      method: input.method,
      createdAt: Date.now(),
      password: input.method === 'password' ? input.password : undefined
    }
    scheduleExpiry(pending.value.createdAt)
  }

  const passwordStash = (pageEmail?: string | null) => {
    const stashValue = pending.value
    if (!stashValue || stashValue.method !== 'password' || !stashValue.password) {
      return null
    }
    if (pageEmail && !emailsEqual(stashValue.email, pageEmail)) {
      return null
    }
    return stashValue
  }

  const isPasskeyStash = (pageEmail?: string | null) => {
    const stashValue = pending.value
    if (!stashValue || stashValue.method !== 'passkey') {
      return false
    }
    if (pageEmail && !emailsEqual(stashValue.email, pageEmail)) {
      return false
    }
    return true
  }

  const broadcastConfirmed = (email?: string | null) => {
    if (!import.meta.client || typeof BroadcastChannel === 'undefined') {
      return
    }
    try {
      const channel = new BroadcastChannel(EMAIL_CONFIRMED_CHANNEL)
      const message: EmailConfirmedMessage = { confirmed: true }
      if (email) {
        message.email = email
      }
      channel.postMessage(message)
      channel.close()
    } catch {
      // BroadcastChannel is optional; same-tab stash login still works.
    }
  }

  const subscribeToConfirmed = (handler: (message: EmailConfirmedMessage) => void) => {
    if (!import.meta.client || typeof BroadcastChannel === 'undefined') {
      return () => {}
    }
    try {
      const channel = new BroadcastChannel(EMAIL_CONFIRMED_CHANNEL)
      const listener = (event: MessageEvent) => {
        if (isEmailConfirmedMessage(event.data)) {
          handler(event.data)
        }
      }
      channel.addEventListener('message', listener)
      return () => {
        channel.removeEventListener('message', listener)
        channel.close()
      }
    } catch {
      return () => {}
    }
  }

  return {
    pending,
    stash,
    clear,
    syncExpiry,
    passwordStash,
    isPasskeyStash,
    broadcastConfirmed,
    subscribeToConfirmed,
    pendingEmailMatches
  }
}
