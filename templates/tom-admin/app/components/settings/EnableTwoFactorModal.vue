<script setup lang="ts">
import type { StepperItem } from '@nuxt/ui'
import type { TwoFactorStatus } from '~/types/auth'

const props = withDefaults(defineProps<{
  showTrigger?: boolean
  email?: string
}>(), {
  showTrigger: true,
  email: ''
})

const emit = defineEmits<{
  success: []
  pendingCodes: [codes: string[]]
}>()

const { api, problemMessage } = useAuth()
const stepUp = useStepUp()
const toast = useToast()
const openModal = ref(false)

const items = ref<StepperItem[]>([
  { slot: 'setup', title: 'Setup' },
  { slot: 'verify', title: 'Verify' },
  { slot: 'finish', title: 'Finish' }
])

const stepper = useTemplateRef('stepper')
const setupSession = ref(0)
const setupResponse = ref<TwoFactorStatus | null>(null)
const setupTotpUri = ref<string | null>(null)
const otp = ref<string[]>([])
const verifying = ref(false)

async function postManage2fa(reauthToken: string, body: Record<string, unknown>) {
  const raw = await api<TwoFactorStatus>('/identity/manage/2fa', {
    method: 'POST',
    body,
    ...reauthRequest(reauthToken)
  })
  return normalizeTwoFactorResponse(raw)
}

async function beginSetup(reauthToken: string) {
  setupResponse.value = await postManage2fa(reauthToken, {
    resetSharedKey: true
  })
  const key = setupResponse.value.sharedKey || ''
  setupTotpUri.value = key ? totpUri(props.email, key) : null
}

async function onEnable() {
  try {
    const done = await stepUp.run(async ({ reauthToken }) => {
      await beginSetup(reauthToken)
      return true
    })
    if (done) {
      otp.value = []
      setupSession.value++
      openModal.value = true
    }
  } catch (error) {
    toast.add({
      title: 'Unable to start two-factor setup',
      description: problemMessage(error, 'An error occurred while processing your request.'),
      color: 'error'
    })
  }
}

function onBeginVerify() {
  stepper.value?.next()
}

async function onSubmitVerify() {
  const code = otp.value.join('')
  if (code.length < 6) {
    toast.add({
      title: 'Enter the 6-digit code',
      color: 'error'
    })
    return
  }

  verifying.value = true
  try {
    const done = await stepUp.run(async ({ reauthToken }) => {
      setupResponse.value = await postManage2fa(reauthToken, {
        enable: true,
        resetRecoveryCodes: true,
        twoFactorCode: code
      })
      return true
    })
    if (!done) {
      return
    }
    toast.add({
      title: 'Two-factor authentication enabled',
      color: 'success'
    })
    emit('success')
    stepper.value?.next()
  } catch (error) {
    toast.add({
      title: 'Unable to enable two-factor',
      description: problemMessage(error, 'Invalid authenticator code.'),
      color: 'error'
    })
  } finally {
    verifying.value = false
  }
}

function acknowledgeRecoveryCodes() {
  if (setupResponse.value?.recoveryCodes?.length) {
    setupResponse.value = {
      ...setupResponse.value,
      recoveryCodes: null
    }
  }
}

function onFinish() {
  acknowledgeRecoveryCodes()
  onModalOpenChange(false)
}

function onModalOpenChange(open: boolean) {
  if (!open) {
    const codes = setupResponse.value?.recoveryCodes
    if (codes?.length) {
      emit('pendingCodes', codes)
    }
    setupResponse.value = null
    setupTotpUri.value = null
    otp.value = []
  }
  openModal.value = open
}
</script>

<template>
  <UButton
    v-if="props.showTrigger"
    color="success"
    variant="subtle"
    label="Enable"
    @click="onEnable"
  />

  <UModal
    v-if="openModal"
    :open="true"
    :dismissible="false"
    title="Setup two-factor authentication"
    @update:open="onModalOpenChange"
  >
    <template #body>
      <UStepper
        :key="setupSession"
        ref="stepper"
        color="neutral"
        :items="items"
        class="w-full"
        disabled
      >
        <template #setup>
          <div class="space-y-4 pt-4">
            <p class="text-sm text-muted">
              Open your authenticator app and scan the QR code, or enter the setup key manually.
            </p>
            <USkeleton
              v-if="!setupResponse"
              class="size-48 rounded-md"
            />
            <SettingsTotpQr
              v-else-if="setupTotpUri"
              :value="setupTotpUri"
            />
            <UFormField label="Setup key">
              <UInput
                :model-value="setupResponse?.sharedKey"
                class="w-full"
                disabled
              />
            </UFormField>
            <UButton
              block
              label="Continue"
              @click="onBeginVerify"
            />
          </div>
        </template>

        <template #verify>
          <form
            class="space-y-4 pt-4"
            @submit.prevent="onSubmitVerify"
          >
            <p class="text-sm text-muted">
              Enter the 6-digit code from your authenticator app.
            </p>
            <UPinInput
              v-model="otp"
              :length="6"
              otp
            />
            <UButton
              type="submit"
              block
              :loading="verifying"
            >
              Verify and enable
            </UButton>
          </form>
        </template>

        <template #finish>
          <div class="space-y-4 pt-4">
            <SettingsRecoveryCodesPanel
              v-if="setupResponse?.recoveryCodes?.length"
              :codes="setupResponse.recoveryCodes ?? []"
            />
            <UButton
              label="Finish"
              trailing-icon="i-lucide-arrow-right"
              block
              @click="onFinish"
            />
          </div>
        </template>
      </UStepper>
    </template>
  </UModal>
</template>
