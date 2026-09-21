<script setup lang="ts">
import type { InfoUpdateRequest, ManageInfo } from '~/types/auth'

const emit = defineEmits<{
  success: []
}>()

const toast = useToast()
const { api, problemMessage } = useAuth()
const stepUp = useStepUp()

const form = reactive({
  oldPassword: '',
  newPassword: '',
  confirmPassword: ''
})
const busy = ref(false)
const errorMessage = ref('')

function resetState() {
  form.oldPassword = ''
  form.newPassword = ''
  form.confirmPassword = ''
}

async function onSubmit() {
  errorMessage.value = ''
  if (!form.oldPassword) {
    errorMessage.value = 'Current password is required.'
    return
  }
  if (form.newPassword.length < 8) {
    errorMessage.value = 'New password must be at least 8 characters.'
    return
  }
  if (form.newPassword !== form.confirmPassword) {
    errorMessage.value = 'Passwords do not match.'
    return
  }

  busy.value = true
  try {
    const body: InfoUpdateRequest = {
      oldPassword: form.oldPassword,
      newPassword: form.newPassword
    }
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

    resetState()
    toast.add({
      title: 'Password updated',
      description: 'Use your new password the next time you sign in.',
      color: 'success'
    })
    emit('success')
  } catch (error) {
    toast.add({
      title: 'Unable to change password',
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
    <UAlert
      v-if="errorMessage"
      color="error"
      variant="subtle"
      :description="errorMessage"
    />
    <UFormField
      label="Current password"
      required
    >
      <UInput
        v-model="form.oldPassword"
        type="password"
        autocomplete="current-password"
        class="w-full"
        required
      />
    </UFormField>
    <UFormField
      label="New password"
      required
    >
      <UInput
        v-model="form.newPassword"
        type="password"
        autocomplete="new-password"
        class="w-full"
        required
      />
    </UFormField>
    <UFormField
      label="Confirm new password"
      required
    >
      <UInput
        v-model="form.confirmPassword"
        type="password"
        autocomplete="new-password"
        class="w-full"
        required
      />
    </UFormField>
    <div class="flex justify-end">
      <UButton
        type="submit"
        label="Change password"
        :loading="busy"
      />
    </div>
  </form>
</template>
