<script setup lang="ts">
import type { TabsItem } from '@nuxt/ui'
import { FetchError } from 'ofetch'
import { createPasskeyCredential } from '~/utils/webauthn'

definePageMeta({
  layout: 'auth',
  middleware: 'guest'
})

useSeoMeta({
  title: 'Create account'
})

const { api, isPasskeyCancel, problemMessage } = useAuth()
const { stash } = usePendingRegistration()
const toast = useToast()

const tabs: TabsItem[] = [
  { label: 'Password', icon: 'i-lucide-lock', slot: 'password' },
  { label: 'Passkey', icon: 'i-lucide-fingerprint', slot: 'passkey' }
]

const passwordForm = reactive({
  email: '',
  password: '',
  confirmPassword: ''
})
const passkeyEmail = ref('')
const busy = ref(false)
const errorMessage = ref('')
const confirmError = ref('')

function goToCheckEmail(email: string) {
  return navigateTo({
    path: '/check-email',
    query: { email }
  })
}

async function registerPassword() {
  errorMessage.value = ''
  confirmError.value = ''

  if (passwordForm.password !== passwordForm.confirmPassword) {
    confirmError.value = 'Passwords do not match.'
    return
  }

  busy.value = true
  try {
    await api('/identity/register', {
      method: 'POST',
      body: {
        email: passwordForm.email,
        password: passwordForm.password
      }
    })
    stash({
      email: passwordForm.email,
      password: passwordForm.password,
      method: 'password'
    })
    await goToCheckEmail(passwordForm.email)
  } catch (error) {
    if (error instanceof FetchError && error.statusCode === 400) {
      errorMessage.value = problemMessage(error, 'Unable to complete registration.')
    } else {
      errorMessage.value = problemMessage(error)
    }
  } finally {
    busy.value = false
  }
}

async function registerPasskey() {
  errorMessage.value = ''
  busy.value = true
  try {
    const optionsJson = await api<string | object>('/account/passkeys/register/options', {
      method: 'POST',
      body: { email: passkeyEmail.value }
    })
    const credentialJson = await createPasskeyCredential(optionsJson)
    await api('/account/passkeys/register', {
      method: 'POST',
      query: { useCookies: true },
      body: {
        email: passkeyEmail.value,
        credentialJson
      }
    })
    stash({
      email: passkeyEmail.value,
      method: 'passkey'
    })
    await goToCheckEmail(passkeyEmail.value)
  } catch (error) {
    if (isPasskeyCancel(error)) {
      toast.add({
        title: 'Passkey cancelled',
        description: 'No account was created.',
        color: 'neutral'
      })
      return
    }
    errorMessage.value = problemMessage(error, 'Unable to complete registration.')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">
        Create account
      </h1>
      <p class="text-sm text-muted">
        Register with a password or a passkey. You will confirm email before signing in.
      </p>
    </div>

    <UAlert
      v-if="errorMessage"
      color="error"
      variant="subtle"
      :description="errorMessage"
    />

    <UTabs
      :items="tabs"
      class="w-full"
    >
      <template #password>
        <form
          class="mt-4 space-y-3"
          @submit.prevent="registerPassword"
        >
          <UFormField
            label="Email"
            required
          >
            <UInput
              v-model="passwordForm.email"
              type="email"
              autocomplete="email"
              class="w-full"
              required
            />
          </UFormField>
          <UFormField
            label="Password"
            required
          >
            <UInput
              v-model="passwordForm.password"
              type="password"
              autocomplete="new-password"
              class="w-full"
              required
            />
          </UFormField>
          <UFormField
            label="Confirm password"
            required
            :error="confirmError || undefined"
          >
            <UInput
              v-model="passwordForm.confirmPassword"
              type="password"
              autocomplete="new-password"
              class="w-full"
              required
            />
          </UFormField>
          <UButton
            type="submit"
            block
            :loading="busy"
          >
            Create account
          </UButton>
        </form>
      </template>

      <template #passkey>
        <form
          class="mt-4 space-y-3"
          @submit.prevent="registerPasskey"
        >
          <UFormField
            label="Email"
            required
          >
            <UInput
              v-model="passkeyEmail"
              type="email"
              autocomplete="email"
              class="w-full"
              required
            />
          </UFormField>
          <UButton
            type="submit"
            block
            :loading="busy"
          >
            Continue with passkey
          </UButton>
        </form>
      </template>
    </UTabs>

    <p class="text-center text-sm text-muted">
      Already have an account?
      <NuxtLink
        to="/login"
        class="text-primary font-medium"
      >
        Sign in
      </NuxtLink>
    </p>
  </div>
</template>
