<script setup lang="ts">
import type { AuthMethodsResponse, ExternalLogin, TwoFactorStatus } from '~/types/auth'

definePageMeta({
  layout: 'app',
  middleware: 'auth'
})

useSeoMeta({
  title: 'Security'
})

type SignInRow = 'email' | 'password' | 'connected'

const toast = useToast()
const { api, info, refreshCookieSession, problemMessage } = useAuth()
const { baseURL } = useApi()
const stepUp = useStepUp()

const twoFactor = ref<TwoFactorStatus | null>(null)
const loadingTwoFactor = ref(true)
const confirmRegenerateOpen = ref(false)
const recoveryCodesOpen = ref(false)
const pendingRecoveryCodes = ref<string[]>([])
const expandedSignIn = ref<SignInRow | undefined>(undefined)

const authMethods = ref<AuthMethodsResponse | null>(null)
const passkeyCount = ref(0)
const loadingPasskeys = ref(true)
const externalLogins = ref<ExternalLogin[]>([])
const externalConfigured = ref(false)
const unlinkTarget = ref<ExternalLogin | null>(null)
const confirmUnlinkOpen = ref(false)

const knownProviders = [
  { scheme: 'Google', label: 'Google', icon: 'i-simple-icons-google' },
  { scheme: 'GitHub', label: 'GitHub', icon: 'i-simple-icons-github' }
] as const

const displayedEmail = computed(() => info.value?.email || '')
const emailConfirmed = computed(() => info.value?.isEmailConfirmed ?? false)
const hasPassword = computed(() => authMethods.value?.password ?? true)

const connectedProviderLabels = computed(() => {
  return knownProviders
    .filter(provider => loginFor(provider.scheme))
    .map(provider => provider.label)
})

const connectedSummary = computed(() => {
  if (!externalConfigured.value) {
    return 'Not configured on this host.'
  }
  if (!connectedProviderLabels.value.length) {
    return 'No connected accounts yet.'
  }
  return connectedProviderLabels.value.join(', ')
})

const passkeySummary = computed(() => {
  if (loadingPasskeys.value) {
    return 'Loading passkeys…'
  }
  if (!passkeyCount.value) {
    return 'No passkeys on this account yet.'
  }
  return `${passkeyCount.value} passkey${passkeyCount.value === 1 ? '' : 's'}`
})

function loginFor(scheme: string) {
  return externalLogins.value.find(item => item.loginProvider.toLowerCase() === scheme.toLowerCase())
}

function disconnectProvider(scheme: string) {
  const login = loginFor(scheme)
  if (login) {
    openUnlinkConfirm(login)
  }
}

async function refreshTwoFactor() {
  loadingTwoFactor.value = true
  try {
    twoFactor.value = normalizeTwoFactorResponse(await api('/identity/manage/2fa'))
  } catch {
    twoFactor.value = null
  } finally {
    loadingTwoFactor.value = false
  }
}

async function refreshAuthMethods() {
  try {
    authMethods.value = normalizeAuthMethods(await api('/identity/manage/authMethods'))
  } catch {
    authMethods.value = null
  }
}

async function refreshPasskeys() {
  loadingPasskeys.value = true
  try {
    passkeyCount.value = readPasskeys(await api('/account/passkeys/')).length
  } catch {
    passkeyCount.value = 0
  } finally {
    loadingPasskeys.value = false
  }
}

async function refreshExternalLogins() {
  try {
    externalLogins.value = normalizeExternalLogins(await api('/auth/external/logins'))
    externalConfigured.value = true
  } catch {
    externalLogins.value = []
    externalConfigured.value = false
  }
}

function showRecoveryCodes(codes: string[]) {
  pendingRecoveryCodes.value = codes
  recoveryCodesOpen.value = true
}

function onRecoveryCodesClosed() {
  pendingRecoveryCodes.value = []
  recoveryCodesOpen.value = false
}

async function onSuccessEnable() {
  await refreshTwoFactor()
}

async function onPasswordChanged() {
  await refreshCookieSession()
  await refreshAuthMethods()
}

function onEnablePendingCodes(codes: string[]) {
  if (codes.length) {
    showRecoveryCodes(codes)
  }
}

async function disableTwoFactor() {
  try {
    const done = await stepUp.run(async ({ reauthToken }) => {
      await api('/identity/manage/2fa', {
        method: 'POST',
        body: { enable: false },
        ...reauthRequest(reauthToken)
      })
      return true
    })

    if (!done) {
      return
    }

    await refreshTwoFactor()
    toast.add({
      title: 'Two-factor authentication disabled',
      color: 'success'
    })
  } catch (error) {
    toast.add({
      title: 'Unable to disable two-factor',
      description: problemMessage(error, 'An error occurred while processing your request.'),
      color: 'error'
    })
  }
}

function openRegenerateConfirm() {
  confirmRegenerateOpen.value = true
}

function closeRegenerateConfirm() {
  confirmRegenerateOpen.value = false
}

async function confirmRegenerateRecoveryCodes() {
  closeRegenerateConfirm()

  try {
    const result = await stepUp.run(async ({ reauthToken }) => {
      return await api<TwoFactorStatus>('/identity/manage/2fa', {
        method: 'POST',
        body: { resetRecoveryCodes: true },
        ...reauthRequest(reauthToken)
      })
    })

    if (!result) {
      return
    }

    const codes = normalizeTwoFactorResponse(result).recoveryCodes
    if (!codes?.length) {
      toast.add({
        title: 'Recovery codes were not returned',
        description: 'Two-factor is still enabled, but no new codes were included in the response.',
        color: 'error'
      })
      await refreshTwoFactor()
      return
    }

    showRecoveryCodes(codes)
    await refreshTwoFactor()
  } catch (error) {
    toast.add({
      title: 'Unable to regenerate recovery codes',
      description: problemMessage(error, 'An error occurred while processing your request.'),
      color: 'error'
    })
  }
}

async function resendEmailConfirmation() {
  try {
    await api('/identity/resendConfirmationEmail', {
      method: 'POST',
      body: displayedEmail.value ? { email: displayedEmail.value } : {}
    })
    toast.add({
      title: 'Confirmation email sent',
      description: 'The link is written to emails/ on the API host.',
      color: 'success'
    })
  } catch (error) {
    toast.add({
      title: 'Unable to resend confirmation',
      description: problemMessage(error, 'An error occurred while processing your request.'),
      color: 'error'
    })
  }
}

function startLink(scheme: string) {
  const returnUrl = `${window.location.origin}/security`
  window.location.href = `${baseURL.value}/auth/external/link/${encodeURIComponent(scheme)}?returnUrl=${encodeURIComponent(returnUrl)}`
}

function openUnlinkConfirm(login: ExternalLogin) {
  unlinkTarget.value = login
  confirmUnlinkOpen.value = true
}

function closeUnlinkConfirm() {
  confirmUnlinkOpen.value = false
  unlinkTarget.value = null
}

async function confirmUnlink() {
  const target = unlinkTarget.value
  closeUnlinkConfirm()
  if (!target) {
    return
  }

  try {
    const done = await stepUp.run(async ({ reauthToken }) => {
      await api(`/auth/external/logins/${encodeURIComponent(target.loginProvider)}/${encodeURIComponent(target.providerKey)}`, {
        method: 'DELETE',
        ...reauthRequest(reauthToken)
      })
      return true
    })

    if (!done) {
      return
    }

    toast.add({
      title: `${target.providerDisplayName || target.loginProvider} disconnected`,
      color: 'success'
    })
    await refreshExternalLogins()
  } catch (error) {
    toast.add({
      title: 'Unable to disconnect account',
      description: problemMessage(error, 'An error occurred while processing your request.'),
      color: 'error'
    })
  }
}

onMounted(async () => {
  await refreshCookieSession()
  await Promise.all([
    refreshTwoFactor(),
    refreshAuthMethods(),
    refreshPasskeys(),
    refreshExternalLogins()
  ])
})
</script>

<template>
  <section class="space-y-6">
    <div class="space-y-1">
      <h1 class="text-2xl font-semibold tracking-tight">
        Security
      </h1>
      <p class="text-sm text-muted">
        Sign-in methods and two-factor authentication. Sensitive changes ask you to confirm your identity first.
      </p>
    </div>

    <UCard>
      <template #header>
        <div>
          <p class="font-semibold">
            Sign-in methods
          </p>
          <p class="text-sm text-muted">
            Email, password, passkeys, and connected accounts you can use to sign in.
          </p>
        </div>
      </template>

      <div class="divide-y divide-default">
        <SettingsSecurityRow
          icon="i-lucide-mail"
          title="Email"
          expandable
          :open="expandedSignIn === 'email'"
          @update:open="expandedSignIn = $event ? 'email' : undefined"
        >
          <template #badge>
            <UBadge
              v-if="emailConfirmed"
              size="md"
              color="primary"
              variant="subtle"
            >
              Verified
            </UBadge>
            <UBadge
              v-else
              size="md"
              color="error"
              variant="subtle"
            >
              Needs confirmation
            </UBadge>
          </template>
          <template #summary>
            {{ displayedEmail || '—' }}
          </template>
          <template #actions>
            <UButton
              v-if="!emailConfirmed"
              color="success"
              variant="subtle"
              label="Resend"
              @click="resendEmailConfirmation"
            />
          </template>
          <p class="text-highlighted mb-1 font-medium">
            Change email
          </p>
          <p class="mb-4 text-xs text-muted">
            We will send a confirmation link to the new address. This email stays the same until you click it.
          </p>
          <SettingsChangeEmailForm
            :current-email="displayedEmail"
            @success="refreshCookieSession"
          />
        </SettingsSecurityRow>

        <SettingsSecurityRow
          icon="i-lucide-lock"
          title="Password"
          expandable
          :open="expandedSignIn === 'password'"
          @update:open="expandedSignIn = $event ? 'password' : undefined"
        >
          <template #badge>
            <UBadge
              v-if="hasPassword"
              size="md"
              color="primary"
              variant="subtle"
            >
              Configured
            </UBadge>
            <UBadge
              v-else
              size="md"
              color="neutral"
              variant="subtle"
            >
              Not configured
            </UBadge>
          </template>
          <template #summary>
            {{ hasPassword ? 'You can sign in with a password.' : 'No password on this account yet.' }}
          </template>
          <SettingsChangePasswordForm @success="onPasswordChanged" />
        </SettingsSecurityRow>

        <SettingsSecurityRow
          icon="i-lucide-fingerprint"
          title="Passkeys"
          to="/passkeys"
        >
          <template #badge>
            <UBadge
              size="md"
              :color="passkeyCount ? 'primary' : 'neutral'"
              variant="subtle"
            >
              {{ loadingPasskeys ? '…' : passkeyCount }}
            </UBadge>
          </template>
          <template #summary>
            {{ passkeySummary }}
          </template>
        </SettingsSecurityRow>

        <SettingsSecurityRow
          icon="i-lucide-link"
          title="Connected accounts"
          :expandable="externalConfigured"
          :open="expandedSignIn === 'connected'"
          :manage-label="connectedProviderLabels.length ? 'Manage' : 'Connect'"
          @update:open="expandedSignIn = $event ? 'connected' : undefined"
        >
          <template #badge>
            <UBadge
              v-if="!externalConfigured"
              size="md"
              color="neutral"
              variant="subtle"
            >
              Not configured
            </UBadge>
            <UBadge
              v-else-if="connectedProviderLabels.length"
              size="md"
              color="primary"
              variant="subtle"
            >
              {{ connectedProviderLabels.length }} connected
            </UBadge>
            <UBadge
              v-else
              size="md"
              color="neutral"
              variant="subtle"
            >
              None connected
            </UBadge>
          </template>
          <template #summary>
            {{ connectedSummary }}
          </template>
          <ul class="space-y-2">
            <li
              v-for="provider in knownProviders"
              :key="provider.scheme"
              class="flex items-center justify-between gap-3 rounded-md border border-default px-3 py-2"
            >
              <div class="flex min-w-0 items-center gap-3">
                <UIcon
                  :name="provider.icon"
                  class="size-5 shrink-0"
                />
                <div class="min-w-0">
                  <p class="text-sm font-medium">
                    {{ provider.label }}
                  </p>
                  <p class="text-xs text-muted">
                    {{ loginFor(provider.scheme) ? 'Connected' : 'Not connected' }}
                  </p>
                </div>
              </div>
              <UButton
                v-if="loginFor(provider.scheme)"
                size="xs"
                color="error"
                variant="subtle"
                label="Disconnect"
                @click="disconnectProvider(provider.scheme)"
              />
              <UButton
                v-else
                size="xs"
                color="neutral"
                variant="subtle"
                label="Connect"
                @click="startLink(provider.scheme)"
              />
            </li>
          </ul>
        </SettingsSecurityRow>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <div>
          <p class="font-semibold">
            Two-factor authentication
          </p>
          <p class="text-sm text-muted">
            Two-factor authentication adds an additional layer of security to your account by requiring more than just a password to sign in.
          </p>
        </div>
      </template>

      <div class="space-y-1">
        <p class="text-sm font-medium">
          Two-factor methods
        </p>
        <SettingsSecurityRow
          icon="i-lucide-smartphone"
          title="Authenticator app"
          description="Use an authentication app or browser extension to get two-factor authentication codes when prompted."
        >
          <template #badge>
            <UBadge
              v-if="twoFactor?.isTwoFactorEnabled"
              size="md"
              color="primary"
              variant="subtle"
            >
              Configured
            </UBadge>
            <UBadge
              v-else
              size="md"
              :color="loadingTwoFactor ? 'neutral' : 'error'"
              variant="subtle"
            >
              {{ loadingTwoFactor ? '…' : 'Not configured' }}
            </UBadge>
          </template>
          <template #actions>
            <UButton
              v-if="twoFactor?.isTwoFactorEnabled"
              color="error"
              variant="subtle"
              label="Disable"
              @click="disableTwoFactor"
            />
            <SettingsEnableTwoFactorModal
              :show-trigger="!loadingTwoFactor && !twoFactor?.isTwoFactorEnabled"
              :email="displayedEmail"
              @success="onSuccessEnable"
              @pending-codes="onEnablePendingCodes"
            />
          </template>
        </SettingsSecurityRow>
      </div>

      <USeparator />

      <div class="space-y-1 pt-3">
        <p class="text-sm font-medium">
          Recovery options
        </p>
        <SettingsSecurityRow
          icon="i-lucide-key-round"
          title="Recovery codes"
          description="Recovery codes can be used to access your account if you lose your authenticator device."
        >
          <template #badge>
            <UBadge
              v-if="twoFactor?.isTwoFactorEnabled"
              size="md"
              color="primary"
              variant="subtle"
            >
              {{ typeof twoFactor.recoveryCodesLeft === 'number' ? `${twoFactor.recoveryCodesLeft} left` : 'Configured' }}
            </UBadge>
            <UBadge
              v-else
              size="md"
              color="neutral"
              variant="subtle"
            >
              Not available
            </UBadge>
          </template>
          <template #actions>
            <UButton
              v-if="twoFactor?.isTwoFactorEnabled"
              color="error"
              variant="subtle"
              label="Regenerate"
              @click="openRegenerateConfirm"
            />
          </template>
        </SettingsSecurityRow>
      </div>
    </UCard>

    <UModal
      v-model:open="confirmRegenerateOpen"
      title="Regenerate recovery codes?"
      description="This replaces your current recovery codes. Old codes stop working immediately."
    >
      <template #body>
        <p class="text-sm text-muted">
          Store the new codes in a safe place. They will only be shown once.
        </p>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            label="Cancel"
            color="neutral"
            variant="subtle"
            @click="closeRegenerateConfirm"
          />
          <UButton
            label="Regenerate"
            color="error"
            @click="confirmRegenerateRecoveryCodes"
          />
        </div>
      </template>
    </UModal>

    <UModal
      v-model:open="confirmUnlinkOpen"
      title="Disconnect this account?"
      :description="unlinkTarget ? `This removes ${unlinkTarget.providerDisplayName || unlinkTarget.loginProvider} as a sign-in method.` : ''"
    >
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            label="Cancel"
            color="neutral"
            variant="subtle"
            @click="closeUnlinkConfirm"
          />
          <UButton
            label="Disconnect"
            color="error"
            @click="confirmUnlink"
          />
        </div>
      </template>
    </UModal>

    <SettingsRecoveryCodesModal
      v-model:open="recoveryCodesOpen"
      :codes="pendingRecoveryCodes"
      @close="onRecoveryCodesClosed"
    />
  </section>
</template>
