<script setup lang="ts">
import type { TabsItem } from '@nuxt/ui'
import { FetchError } from 'ofetch'
import type { AuthMethodsResponse, ConfirmIdentityBody, ConfirmIdentityResponse, StepUpMethod } from '~/types/auth'
import { getPasskeyCredential } from '~/utils/webauthn'

const emit = defineEmits<{
  close: [result?: { reauthToken: string }]
}>()

const { api, isPasskeyCancel } = useAuth()
const toast = useToast()

const resolved = ref(false)
const loadingMethods = ref(true)
const submitting = ref(false)
const methodsError = ref('')
const availableMethods = ref<StepUpMethod[]>([])

const password = ref('')
const twoFactorCode = ref<string[]>([])
const recoveryCode = ref('')

const methodTabs = computed<TabsItem[]>(() => {
  const tabs: TabsItem[] = []
  if (availableMethods.value.includes('password')) {
    tabs.push({
      label: 'Password',
      icon: 'i-lucide-key-round',
      slot: 'password',
      value: 'password'
    })
  }
  if (availableMethods.value.includes('passkeys')) {
    tabs.push({
      label: 'Passkey',
      icon: 'i-lucide-fingerprint',
      slot: 'passkey',
      value: 'passkey'
    })
  }
  if (availableMethods.value.includes('authenticator')) {
    tabs.push({
      label: 'Authenticator',
      icon: 'i-lucide-smartphone',
      slot: 'authenticator',
      value: 'authenticator'
    })
  }
  if (availableMethods.value.includes('recoveryCodes')) {
    tabs.push({
      label: 'Recovery code',
      icon: 'i-lucide-refresh-ccw-dot',
      slot: 'recovery',
      value: 'recovery'
    })
  }
  return tabs
})

function finish(result?: { reauthToken: string }) {
  if (resolved.value) {
    return
  }
  resolved.value = true
  emit('close', result)
}

function clearProof() {
  password.value = ''
  twoFactorCode.value = []
  recoveryCode.value = ''
}

function onOpenChange(open: boolean) {
  if (!open) {
    clearProof()
    finish()
  }
}

async function loadMethods() {
  loadingMethods.value = true
  methodsError.value = ''
  try {
    const raw = await api<AuthMethodsResponse>('/identity/manage/authMethods')
    availableMethods.value = visibleStepUpMethods(normalizeAuthMethods(raw))
    if (availableMethods.value.length === 0) {
      methodsError.value = 'No confirmation methods are available for this account.'
    }
  } catch {
    methodsError.value = 'Unable to load confirmation methods.'
    toast.add({
      title: 'Unable to load confirmation methods',
      color: 'error'
    })
  } finally {
    loadingMethods.value = false
  }
}

async function postConfirm(body: ConfirmIdentityBody) {
  const response = await api<ConfirmIdentityResponse>('/identity/confirmIdentity', {
    method: 'POST',
    body
  })
  const reauthToken = readReauthToken(response)
  clearProof()
  finish({ reauthToken })
}

async function confirm(body: ConfirmIdentityBody) {
  if (submitting.value) {
    return
  }

  submitting.value = true
  try {
    await postConfirm(body)
  } catch (error) {
    clearProof()
    if (error instanceof FetchError && fetchStatus(error) === 401) {
      toast.add({
        title: 'Could not confirm identity',
        description: 'Check your details and try again.',
        color: 'error'
      })
      return
    }
    toast.add({
      title: 'Could not confirm identity',
      description: problemMessage(error, 'An error occurred while processing your request.'),
      color: 'error'
    })
  } finally {
    submitting.value = false
  }
}

async function submitPassword() {
  const trimmed = password.value.trim()
  if (!trimmed) {
    toast.add({
      title: 'Enter your password',
      color: 'error'
    })
    return
  }
  await confirm(buildConfirmIdentityBody('password', trimmed))
}

async function submitAuthenticator() {
  const trimmed = twoFactorCode.value.join('')
  if (!trimmed) {
    toast.add({
      title: 'Enter your authenticator code',
      color: 'error'
    })
    return
  }
  await confirm(buildConfirmIdentityBody('authenticator', trimmed))
}

async function submitRecovery() {
  const trimmed = recoveryCode.value.trim()
  if (!trimmed) {
    toast.add({
      title: 'Enter a recovery code',
      color: 'error'
    })
    return
  }
  await confirm(buildConfirmIdentityBody('recoveryCodes', trimmed))
}

async function submitPasskey() {
  if (submitting.value) {
    return
  }

  submitting.value = true
  try {
    const optionsJson = await api<string | object>('/identity/confirmIdentity/passkeyOptions', {
      method: 'POST',
      body: {}
    })
    const credentialJson = await getPasskeyCredential(optionsJson)
    try {
      await postConfirm({ credentialJson })
    } catch (error) {
      clearProof()
      if (error instanceof FetchError && fetchStatus(error) === 401) {
        toast.add({
          title: 'Could not confirm identity',
          description: 'Check your details and try again.',
          color: 'error'
        })
        return
      }
      toast.add({
        title: 'Could not confirm identity',
        description: problemMessage(error, 'An error occurred while processing your request.'),
        color: 'error'
      })
    }
  } catch (error) {
    if (isPasskeyCancel(error)) {
      toast.add({
        title: 'Passkey cancelled',
        color: 'neutral'
      })
      return
    }
    toast.add({
      title: 'Could not confirm identity',
      description: problemMessage(error, 'Passkey confirmation failed.'),
      color: 'error'
    })
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  loadMethods()
})
</script>

<template>
  <UModal
    :default-open="true"
    title="Confirm your identity"
    description="This action needs a recent identity confirmation."
    :close="{ onClick: () => finish() }"
    @update:open="onOpenChange"
  >
    <template #body>
      <p
        v-if="loadingMethods"
        class="text-sm text-muted"
      >
        Loading confirmation methods…
      </p>

      <UAlert
        v-else-if="methodsError"
        color="error"
        variant="subtle"
        icon="i-lucide-info"
        :title="methodsError"
      />

      <UTabs
        v-else
        :items="methodTabs"
        variant="pill"
        size="sm"
        class="w-full"
      >
        <template #password>
          <form
            class="mt-4 space-y-3"
            @submit.prevent="submitPassword"
          >
            <p class="text-sm text-muted">
              Enter your account password to continue.
            </p>
            <UInput
              v-model="password"
              type="password"
              autocomplete="current-password"
              placeholder="Password"
              class="w-full"
            />
            <UButton
              type="submit"
              block
              :loading="submitting"
            >
              Confirm
            </UButton>
          </form>
        </template>

        <template #passkey>
          <form
            class="mt-4 space-y-3"
            @submit.prevent="submitPasskey"
          >
            <p class="text-sm text-muted">
              Use a passkey registered on this account.
            </p>
            <UButton
              type="submit"
              block
              icon="i-lucide-fingerprint"
              :loading="submitting"
            >
              Continue with passkey
            </UButton>
          </form>
        </template>

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
              :loading="submitting"
            >
              Confirm
            </UButton>
          </form>
        </template>

        <template #recovery>
          <form
            class="mt-4 space-y-3"
            @submit.prevent="submitRecovery"
          >
            <UAlert
              color="warning"
              variant="subtle"
              icon="i-lucide-triangle-alert"
              title="Using a recovery code consumes it"
              description="Each recovery code can be used only once. Prefer your authenticator app when you can."
            />
            <UInput
              v-model="recoveryCode"
              autocomplete="off"
              placeholder="Recovery code"
              class="w-full"
            />
            <UButton
              type="submit"
              block
              :loading="submitting"
            >
              Confirm
            </UButton>
          </form>
        </template>
      </UTabs>
    </template>

    <template #footer>
      <div class="flex justify-end">
        <UButton
          label="Cancel"
          color="neutral"
          variant="subtle"
          @click="finish()"
        />
      </div>
    </template>
  </UModal>
</template>
