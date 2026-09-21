<script setup lang="ts">
import type { TabsItem } from '@nuxt/ui'
import { getPasskeyCredential } from '~/utils/webauthn'

definePageMeta({
  layout: 'auth',
  middleware: 'guest'
})

useSeoMeta({
  title: 'Sign in'
})

const route = useRoute()
const {
  api,
  loginWithPassword,
  loginFailureMessage,
  isPasskeyCancel,
  isRequiresTwoFactor,
  isLockedOut,
  safeAppRedirect
} = useAuth()
const { pending, clear } = usePendingRegistration()
const toast = useToast()

const tabs: TabsItem[] = [
  { label: 'Password', icon: 'i-lucide-lock', slot: 'password', value: 'password' },
  { label: 'Passkey', icon: 'i-lucide-fingerprint', slot: 'passkey', value: 'passkey' }
]

const selectedTab = computed(() => route.query.method === 'passkey' ? 'passkey' : 'password')

const passwordForm = reactive({
  email: '',
  password: '',
  rememberMe: true
})
const passkeyUsername = ref('')
const busy = ref(false)
const errorMessage = ref('')
const openTfaModal = ref(false)
const twoFactorCode = ref<string[]>([])
const twoFactorRecoveryCode = ref('')

const tfaTabs: TabsItem[] = [
  { label: 'Authenticator', icon: 'i-lucide-smartphone', slot: 'authenticator' },
  { label: 'Recovery code', icon: 'i-lucide-refresh-ccw-dot', slot: 'recovery' }
]

const queryEmail = computed(() => {
  const value = route.query.email
  return typeof value === 'string' ? value : ''
})

if (queryEmail.value) {
  passwordForm.email = queryEmail.value
  passkeyUsername.value = queryEmail.value
} else if (pending.value?.email) {
  passwordForm.email = pending.value.email
  passkeyUsername.value = pending.value.email
}

const forgotQuery = computed(() => passwordForm.email ? { email: passwordForm.email } : undefined)

async function afterLogin() {
  clear()
  openTfaModal.value = false
  await navigateTo(safeAppRedirect(route.query.redirect))
}

function loginError(error: unknown, fromTwoFactor: boolean) {
  if (isRequiresTwoFactor(error)) {
    errorMessage.value = fromTwoFactor ? 'Two-factor authentication failed.' : ''
    openTfaModal.value = true
    if (fromTwoFactor) {
      toast.add({
        title: 'Two-factor authentication failed',
        color: 'error'
      })
    }
    return
  }
  if (isLockedOut(error)) {
    errorMessage.value = 'Account locked out. Try again later.'
    toast.add({
      title: 'Account locked out',
      description: 'Too many failed attempts, please try again later.',
      color: 'error'
    })
    return
  }
  errorMessage.value = loginFailureMessage(error)
}

async function loginPassword(extra?: { twoFactorCode?: string, twoFactorRecoveryCode?: string }) {
  errorMessage.value = ''
  busy.value = true
  try {
    await loginWithPassword(
      passwordForm.email,
      passwordForm.password,
      passwordForm.rememberMe,
      extra
    )
    await afterLogin()
  } catch (error) {
    loginError(error, Boolean(extra?.twoFactorCode || extra?.twoFactorRecoveryCode))
  } finally {
    busy.value = false
  }
}

async function submitAuthenticator() {
  const code = twoFactorCode.value.join('')
  if (!code) {
    toast.add({ title: 'Enter the 6-digit code', color: 'error' })
    return
  }
  await loginPassword({ twoFactorCode: code })
}

async function submitRecovery() {
  const code = twoFactorRecoveryCode.value.trim()
  if (!code) {
    toast.add({ title: 'Enter a recovery code', color: 'error' })
    return
  }
  await loginPassword({ twoFactorRecoveryCode: code })
}

async function loginPasskey() {
  errorMessage.value = ''
  busy.value = true
  try {
    const optionsJson = await api<string | object>('/account/passkeys/requestOptions', {
      method: 'POST',
      body: passkeyUsername.value ? { email: passkeyUsername.value } : {}
    })
    const credentialJson = await getPasskeyCredential(optionsJson)
    await api('/account/passkeys/login', {
      method: 'POST',
      query: { useCookies: true },
      body: { credentialJson }
    })
    await afterLogin()
  } catch (error) {
    if (isPasskeyCancel(error)) {
      toast.add({
        title: 'Passkey cancelled',
        color: 'neutral'
      })
      return
    }
    errorMessage.value = loginFailureMessage(error)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">
        Sign in
      </h1>
      <p class="text-sm text-muted">
        Use your password or a passkey. Accounts must be confirmed first.
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
      :default-value="selectedTab"
      class="w-full"
    >
      <template #password>
        <form
          class="mt-4 space-y-3"
          @submit.prevent="loginPassword()"
        >
          <UFormField
            label="Email"
            required
          >
            <UInput
              v-model="passwordForm.email"
              type="email"
              autocomplete="username"
              class="w-full"
              required
            />
          </UFormField>
          <UFormField
            label="Password"
            required
          >
            <template #hint>
              <NuxtLink
                :to="{ path: '/forgot-password', query: forgotQuery }"
                class="text-primary text-xs font-medium"
                tabindex="-1"
              >
                Forgot password?
              </NuxtLink>
            </template>
            <UInput
              v-model="passwordForm.password"
              type="password"
              autocomplete="current-password"
              class="w-full"
              required
            />
          </UFormField>
          <UCheckbox
            v-model="passwordForm.rememberMe"
            label="Remember me"
          />
          <UButton
            type="submit"
            block
            :loading="busy"
          >
            Sign in
          </UButton>
        </form>
      </template>

      <template #passkey>
        <form
          class="mt-4 space-y-3"
          @submit.prevent="loginPasskey"
        >
          <UFormField
            label="Username"
            hint="Optional"
          >
            <UInput
              v-model="passkeyUsername"
              autocomplete="username"
              class="w-full"
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
      Need an account?
      <NuxtLink
        to="/register"
        class="text-primary font-medium"
      >
        Register
      </NuxtLink>
    </p>

    <UModal
      v-model:open="openTfaModal"
      :dismissible="false"
      title="Two-factor authentication"
      description="Enter an authenticator code or a recovery code to finish signing in."
    >
      <template #body>
        <UTabs
          :items="tfaTabs"
          variant="pill"
          size="sm"
        >
          <template #authenticator>
            <form
              class="mt-4 space-y-3"
              @submit.prevent="submitAuthenticator"
            >
              <p class="text-sm text-muted">
                Enter the 6-digit code from your authenticator app.
              </p>
              <UPinInput
                v-model="twoFactorCode"
                :length="6"
                otp
              />
              <UButton
                type="submit"
                block
                :loading="busy"
              >
                Continue
              </UButton>
            </form>
          </template>
          <template #recovery>
            <form
              class="mt-4 space-y-3"
              @submit.prevent="submitRecovery"
            >
              <p class="text-sm text-muted">
                Enter one of your recovery codes.
              </p>
              <UInput
                v-model="twoFactorRecoveryCode"
                autocomplete="off"
                class="w-full"
              />
              <UButton
                type="submit"
                block
                :loading="busy"
              >
                Continue
              </UButton>
            </form>
          </template>
        </UTabs>
      </template>
    </UModal>
  </div>
</template>
