<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  middleware: 'guest'
})

useSeoMeta({
  title: 'Reset password'
})

const route = useRoute()
const { api, problemMessage } = useAuth()
const toast = useToast()

function firstQuery(name: string) {
  const value = route.query[name]
  return typeof value === 'string' ? value : ''
}

const form = reactive({
  email: firstQuery('email'),
  resetCode: firstQuery('resetCode') || firstQuery('code'),
  newPassword: '',
  confirmPassword: ''
})
const busy = ref(false)
const errorMessage = ref('')

const emailedCodeHint = computed(() => {
  return Boolean(firstQuery('email')) && !firstQuery('resetCode') && !firstQuery('code')
})

async function onSubmit() {
  errorMessage.value = ''
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
    await api('/identity/resetPassword', {
      method: 'POST',
      body: {
        email: form.email,
        resetCode: form.resetCode,
        newPassword: form.newPassword
      },
      skipCsrf: true
    })
    toast.add({
      title: 'Password reset',
      description: 'You can now sign in with your new password.',
      color: 'success'
    })
    await navigateTo({
      path: '/login',
      query: form.email ? { email: form.email } : undefined
    })
  } catch (error) {
    errorMessage.value = problemMessage(error, 'Unable to reset password.')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">
        Reset password
      </h1>
      <p class="text-sm text-muted">
        {{ emailedCodeHint
          ? 'Enter the code from emails/ on the API host, then choose a new password.'
          : 'Enter the email, reset code, and a new password.' }}
      </p>
    </div>

    <UAlert
      v-if="errorMessage"
      color="error"
      variant="subtle"
      :description="errorMessage"
    />

    <form
      class="space-y-3"
      @submit.prevent="onSubmit"
    >
      <UFormField
        label="Email"
        required
      >
        <UInput
          v-model="form.email"
          type="email"
          autocomplete="email"
          class="w-full"
          required
        />
      </UFormField>
      <UFormField
        label="Reset code"
        required
      >
        <UInput
          v-model="form.resetCode"
          autocomplete="off"
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
        label="Confirm password"
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
      <UButton
        type="submit"
        block
        :loading="busy"
      >
        Reset password
      </UButton>
    </form>

    <p class="text-center text-sm text-muted">
      Remember your password?
      <NuxtLink
        to="/login"
        class="text-primary font-medium"
      >
        Sign in
      </NuxtLink>
    </p>
  </div>
</template>
