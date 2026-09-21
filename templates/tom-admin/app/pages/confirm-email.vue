<script setup lang="ts">
definePageMeta({
  layout: 'auth'
})

useSeoMeta({
  title: 'Email confirmation'
})

const route = useRoute()
const { loginWithPassword, loginFailureMessage } = useAuth()
const { pending, clear, syncExpiry, passwordStash, broadcastConfirmed } = usePendingRegistration()

function firstQuery(name: string) {
  const value = route.query[name]
  return typeof value === 'string' ? value : ''
}

const status = computed(() => firstQuery('status'))
const flow = computed(() => firstQuery('flow'))

const isConfirmed = computed(() => status.value === 'confirmed')
const isFailed = computed(() => status.value === 'failed')
const isChangeEmail = computed(() => flow.value === 'change-email')

const signingIn = ref(false)
const signInError = ref('')
const autoLoginAttempted = ref(false)

const title = computed(() => {
  if (isConfirmed.value) {
    return isChangeEmail.value ? 'Email updated' : 'Email confirmed'
  }
  if (isFailed.value) {
    return isChangeEmail.value ? 'Email change failed' : 'Email confirmation failed'
  }
  return 'Email confirmation'
})

const description = computed(() => {
  if (signingIn.value) {
    return 'Your email is confirmed. Signing you in…'
  }
  if (isConfirmed.value) {
    if (signInError.value) {
      return 'Your email is confirmed, but automatic sign-in did not succeed.'
    }
    if (isChangeEmail.value) {
      return 'Your email address has been updated. Sign in with your new email.'
    }
    return 'Your email has been confirmed. You can now sign in.'
  }
  if (isFailed.value) {
    if (isChangeEmail.value) {
      return 'We could not update your email. The link may be invalid or expired. Sign in and request a new change-email confirmation.'
    }
    return 'We could not confirm your email. The link may be invalid or expired. Register again, or sign in if you already confirmed.'
  }
  return 'Open this page from the confirmation redirect, or sign in if you already confirmed your email.'
})

const showSignIn = computed(() => {
  if (signingIn.value) {
    return false
  }
  const waitingForAutoLogin = isConfirmed.value
    && !isChangeEmail.value
    && !signInError.value
    && !!passwordStash()
    && !autoLoginAttempted.value
  return !waitingForAutoLogin
})

const signInLabel = computed(() => {
  if (isChangeEmail.value) {
    return 'Sign in with new email'
  }
  return 'Sign in'
})

async function tryStashedPasswordLogin() {
  const stash = passwordStash()
  if (!stash?.password || autoLoginAttempted.value || signingIn.value) {
    return
  }

  autoLoginAttempted.value = true
  signingIn.value = true
  signInError.value = ''
  try {
    await loginWithPassword(stash.email, stash.password)
    clear()
    await navigateTo('/')
  } catch (error) {
    signInError.value = loginFailureMessage(error)
  } finally {
    signingIn.value = false
  }
}

onMounted(() => {
  syncExpiry()
  if (!isConfirmed.value) {
    return
  }

  if (isChangeEmail.value) {
    return
  }

  broadcastConfirmed(pending.value?.email)
  void tryStashedPasswordLogin()
})
</script>

<template>
  <div class="space-y-4">
    <UAlert
      :color="isConfirmed ? 'success' : isFailed ? 'error' : 'neutral'"
      variant="subtle"
      :title="title"
      :description="description"
      :icon="isConfirmed ? 'i-lucide-circle-check' : isFailed ? 'i-lucide-circle-alert' : 'i-lucide-mail'"
    />

    <UAlert
      v-if="signInError"
      color="error"
      variant="subtle"
      :description="signInError"
    />

    <UButton
      v-if="showSignIn"
      to="/login"
      :label="signInLabel"
      block
    />
  </div>
</template>
