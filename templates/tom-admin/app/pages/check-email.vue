<script setup lang="ts">
definePageMeta({
  layout: 'auth'
})

useSeoMeta({
  title: 'Check your email'
})

const route = useRoute()
const { confirmEmail, loginWithPassword, loginFailureMessage } = useAuth()
const {
  pending,
  clear,
  syncExpiry,
  passwordStash,
  isPasskeyStash,
  subscribeToConfirmed,
  pendingEmailMatches
} = usePendingRegistration()
const toast = useToast()

const email = computed(() => {
  const value = route.query.email
  return typeof value === 'string' ? value : ''
})

const confirmForm = reactive({
  userId: '',
  code: '',
  changedEmail: ''
})
const pastedUrl = ref('')
const busy = ref(false)
const signingIn = ref(false)
const signInError = ref('')
const autoLoginAttempted = ref(false)

const hasPasswordStash = computed(() => !!passwordStash(email.value || null))
const hasPasskeyStash = computed(() => isPasskeyStash(email.value || null))

const waitingCopy = computed(() => {
  if (hasPasskeyStash.value) {
    return 'we sent a confirmation link. Confirm from the API emails/ folder (or paste the link below), then continue with your passkey. This page does not sign you in automatically.'
  }
  if (hasPasswordStash.value) {
    return 'we sent a confirmation link. The message is written to emails/ on the API host. After you confirm in this browser, we can sign you in once — this page does not retry login.'
  }
  return 'we sent a confirmation link. The message is written to emails/ on the API host.'
})

const loginQuery = computed(() => email.value ? { email: email.value } : undefined)
const passkeyLoginQuery = computed(() => ({
  ...(loginQuery.value ?? {}),
  method: 'passkey'
}))

function parseConfirmLink() {
  const raw = pastedUrl.value.trim() || window.prompt('Paste the full confirmation URL from emails/') || ''
  if (!raw) {
    return
  }

  try {
    const url = new URL(raw.replace(/&amp;/g, '&'))
    confirmForm.userId = url.searchParams.get('userId') ?? ''
    confirmForm.code = url.searchParams.get('code') ?? ''
    confirmForm.changedEmail = url.searchParams.get('changedEmail') ?? ''
    pastedUrl.value = raw
    toast.add({ title: 'Parsed confirmation link', color: 'success' })
  } catch {
    toast.add({ title: 'Invalid URL', color: 'error' })
  }
}

async function submitConfirm() {
  if (!confirmForm.userId || !confirmForm.code) {
    parseConfirmLink()
    if (!confirmForm.userId || !confirmForm.code) {
      return
    }
  }

  busy.value = true
  try {
    const result = await confirmEmail({
      userId: confirmForm.userId,
      code: confirmForm.code,
      changedEmail: confirmForm.changedEmail || undefined
    })
    await navigateTo({
      path: '/confirm-email',
      query: {
        status: result.status,
        flow: result.flow
      }
    })
  } finally {
    busy.value = false
  }
}

async function tryPasswordLoginOnce(source: 'auto' | 'manual') {
  const stash = passwordStash(email.value || null)
  if (!stash?.password) {
    if (source === 'manual') {
      await navigateTo({
        path: '/login',
        query: loginQuery.value
      })
    }
    return
  }

  if (source === 'auto' && autoLoginAttempted.value) {
    return
  }
  if (signingIn.value) {
    return
  }
  if (source === 'auto') {
    autoLoginAttempted.value = true
  }

  signingIn.value = true
  signInError.value = ''
  try {
    await loginWithPassword(stash.email, stash.password)
    clear()
    await navigateTo('/')
  } catch (error) {
    signInError.value = source === 'auto'
      ? `${loginFailureMessage(error)} Confirm your email, then sign in.`
      : loginFailureMessage(error)
  } finally {
    signingIn.value = false
  }
}

async function onConfirmedSignIn() {
  await tryPasswordLoginOnce('manual')
}

async function useDifferentEmail() {
  clear()
  await navigateTo('/register')
}

let unsubscribeConfirmed = () => {}

onMounted(() => {
  syncExpiry()
  unsubscribeConfirmed = subscribeToConfirmed((message) => {
    const stash = pending.value
    if (!stash) {
      return
    }
    if (!pendingEmailMatches(stash.email, message.email)) {
      return
    }
    if (stash.method === 'passkey') {
      toast.add({
        title: 'Email confirmed',
        description: 'Continue with your passkey to sign in.',
        color: 'success'
      })
      return
    }
    void tryPasswordLoginOnce('auto')
  })
})

onUnmounted(() => {
  unsubscribeConfirmed()
})
</script>

<template>
  <div class="space-y-4">
    <div class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">
        Check your email
      </h1>
      <p class="text-sm text-muted">
        If an account can be created for
        <span class="font-medium">{{ email || 'that address' }}</span>,
        {{ waitingCopy }}
      </p>
    </div>

    <UAlert
      v-if="signInError"
      color="error"
      variant="subtle"
      :description="signInError"
    />

    <div class="flex flex-col gap-2">
      <UButton
        v-if="hasPasskeyStash"
        :to="{ path: '/login', query: passkeyLoginQuery }"
        block
      >
        Continue with passkey
      </UButton>
      <UButton
        v-else
        block
        :loading="signingIn"
        @click="onConfirmedSignIn"
      >
        I’ve confirmed — sign in
      </UButton>
      <UButton
        v-if="hasPasskeyStash"
        :to="{ path: '/login', query: loginQuery }"
        color="neutral"
        variant="outline"
        block
      >
        Sign in
      </UButton>
      <UButton
        color="neutral"
        variant="outline"
        block
        @click="useDifferentEmail"
      >
        Use a different email
      </UButton>
    </div>

    <UCollapsible class="flex flex-col gap-2">
      <UButton
        label="Local dev: paste confirmation link"
        color="neutral"
        variant="subtle"
        trailing-icon="i-lucide-chevron-down"
        block
      />
      <template #content>
        <div class="space-y-3 pt-1">
          <p class="text-xs text-muted">
            Copy the confirmation URL from emails/ on the API host, then confirm here.
          </p>
          <UFormField label="Confirmation URL">
            <UInput
              v-model="pastedUrl"
              class="w-full"
              placeholder="http://localhost:5080/identity/confirmEmail?..."
            />
          </UFormField>
          <div class="flex flex-wrap gap-2">
            <UButton
              size="sm"
              color="neutral"
              variant="outline"
              icon="i-lucide-clipboard-paste"
              @click="parseConfirmLink"
            >
              Parse link
            </UButton>
            <UButton
              size="sm"
              :loading="busy"
              :disabled="!confirmForm.userId || !confirmForm.code"
              @click="submitConfirm"
            >
              Confirm email
            </UButton>
          </div>
        </div>
      </template>
    </UCollapsible>
  </div>
</template>
