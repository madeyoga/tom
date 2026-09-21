<script setup lang="ts">
import type { InfoUpdateRequest, ManageInfo } from '~/types/auth'

const props = defineProps<{
  currentEmail: string
}>()

const emit = defineEmits<{
  success: []
}>()

const toast = useToast()
const { api, problemMessage } = useAuth()
const stepUp = useStepUp()

const newEmail = ref('')
const busy = ref(false)
const errorMessage = ref('')

async function onSubmit() {
  errorMessage.value = ''
  const next = newEmail.value.trim()
  if (!next) {
    errorMessage.value = 'Enter a new email.'
    return
  }
  if (next.toLowerCase() === props.currentEmail.trim().toLowerCase()) {
    errorMessage.value = 'Enter a different email than your current address.'
    return
  }

  busy.value = true
  try {
    const body: InfoUpdateRequest = { newEmail: next }
    const done = await stepUp.run(async ({ reauthToken }) => {
      await api<ManageInfo>('/identity/manage/info', {
        method: 'POST',
        body,
        ...reauthRequest(reauthToken)
      })
      return true
    })

    if (!done) {
      return
    }

    newEmail.value = ''
    toast.add({
      title: 'Check your inbox',
      description: 'We sent a confirmation link to the new address. Your current email stays in effect until you click it. The link is written to emails/ on the API host.',
      color: 'success'
    })
    emit('success')
  } catch (error) {
    toast.add({
      title: 'Unable to change email',
      description: problemMessage(error, 'An error occurred while processing your request.'),
      color: 'error'
    })
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <form
    class="space-y-3"
    @submit.prevent="onSubmit"
  >
    <UFormField
      label="New email"
      description="Your email will not change until you confirm the new address."
      :error="errorMessage || undefined"
      required
    >
      <UInput
        v-model="newEmail"
        type="email"
        autocomplete="email"
        class="w-full"
        placeholder="you@example.com"
        required
      />
    </UFormField>
    <div class="flex justify-end">
      <UButton
        type="submit"
        label="Change email"
        :loading="busy"
      />
    </div>
  </form>
</template>
