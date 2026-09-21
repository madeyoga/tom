<script setup lang="ts">
import type { PasskeyCredential } from '~/types/auth'
import { createPasskeyCredential } from '~/utils/webauthn'

definePageMeta({
  layout: 'app',
  middleware: 'auth'
})

useSeoMeta({
  title: 'Passkeys'
})

const { api, isPasskeyCancel, problemMessage } = useAuth()
const stepUp = useStepUp()
const toast = useToast()

const passkeys = ref<PasskeyCredential[]>([])
const loading = ref(true)
const busy = ref(false)

async function listPasskeys() {
  loading.value = true
  try {
    const response = await api('/account/passkeys/')
    passkeys.value = readPasskeys(response)
  } catch (error) {
    toast.add({
      title: 'Unable to load passkeys',
      description: problemMessage(error, 'An error occurred while processing your request.'),
      color: 'error'
    })
  } finally {
    loading.value = false
  }
}

async function addPasskey() {
  busy.value = true
  try {
    const done = await stepUp.run(async ({ reauthToken }) => {
      const optionsJson = await api<string | object>('/account/passkeys/creationOptions', {
        method: 'POST',
        body: {},
        ...reauthRequest(reauthToken)
      })
      const credentialJson = await createPasskeyCredential(optionsJson)
      await api('/account/passkeys/', {
        method: 'POST',
        body: { credentialJson },
        ...reauthRequest(reauthToken)
      })
      return true
    })

    if (!done) {
      return
    }

    toast.add({
      title: 'Passkey added',
      color: 'success'
    })
    await listPasskeys()
  } catch (error) {
    if (isPasskeyCancel(error)) {
      toast.add({
        title: 'Passkey cancelled',
        color: 'neutral'
      })
      return
    }
    toast.add({
      title: 'Unable to add passkey',
      description: problemMessage(error, 'An error occurred while processing your request.'),
      color: 'error'
    })
  } finally {
    busy.value = false
  }
}

async function removePasskey(credentialId: string) {
  busy.value = true
  try {
    const done = await stepUp.run(async ({ reauthToken }) => {
      await api(`/account/passkeys/${encodeURIComponent(credentialId)}`, {
        method: 'DELETE',
        ...reauthRequest(reauthToken)
      })
      return true
    })

    if (!done) {
      return
    }

    toast.add({
      title: 'Passkey removed',
      color: 'success'
    })
    await listPasskeys()
  } catch (error) {
    toast.add({
      title: 'Unable to remove passkey',
      description: problemMessage(error, 'An error occurred while processing your request.'),
      color: 'error'
    })
  } finally {
    busy.value = false
  }
}

onMounted(() => {
  listPasskeys()
})
</script>

<template>
  <section class="space-y-4">
    <div class="flex items-start justify-between gap-3">
      <div class="space-y-1">
        <h1 class="text-2xl font-semibold tracking-tight">
          Passkeys
        </h1>
        <p class="text-sm text-muted">
          Passkeys registered on this account. Adding or deleting asks you to confirm your identity. Register and sign-in with a passkey stay on the auth pages.
        </p>
      </div>
      <UButton
        icon="i-lucide-plus"
        :loading="busy"
        @click="addPasskey"
      >
        Add passkey
      </UButton>
    </div>

    <UAlert
      color="info"
      variant="subtle"
      title="Browser + localhost"
      description="Passkeys.ServerDomain is localhost. Use Chrome/Edge on https or http://localhost."
    />

    <UCard>
      <template #header>
        <div>
          <p class="font-semibold">
            Registered passkeys
          </p>
          <p class="text-sm text-muted">
            {{ loading ? 'Loading…' : passkeys.length ? `${passkeys.length} passkey${passkeys.length === 1 ? '' : 's'}` : 'No passkeys on this account yet.' }}
          </p>
        </div>
      </template>
      <ul
        v-if="passkeys.length"
        class="space-y-2"
      >
        <li
          v-for="item in passkeys"
          :key="item.credentialId"
          class="flex items-center justify-between gap-2 rounded-md border border-default px-3 py-2"
        >
          <div class="min-w-0">
            <p class="truncate text-sm font-medium">
              {{ item.displayName || 'Passkey' }}
            </p>
            <p class="truncate font-mono text-xs text-muted">
              {{ item.credentialId }}
            </p>
          </div>
          <UButton
            size="xs"
            color="error"
            variant="ghost"
            icon="i-lucide-trash-2"
            :loading="busy"
            @click="removePasskey(item.credentialId)"
          />
        </li>
      </ul>
      <p
        v-else-if="!loading"
        class="text-sm text-muted"
      >
        Add a passkey to sign in without a password on this device.
      </p>
    </UCard>
  </section>
</template>
