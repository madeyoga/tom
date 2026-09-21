<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  middleware: 'guest'
})

useSeoMeta({
  title: 'Forgot password'
})

const route = useRoute()
const { api, problemMessage } = useAuth()
const toast = useToast()

const email = ref(typeof route.query.email === 'string' ? route.query.email : '')
const busy = ref(false)
const errorMessage = ref('')

async function onSubmit() {
  errorMessage.value = ''
  const value = email.value.trim()
  if (!value) {
    errorMessage.value = 'Enter your email.'
    return
  }

  busy.value = true
  try {
    await api('/identity/forgotPassword', {
      method: 'POST',
      body: { email: value },
      skipCsrf: true
    })
    toast.add({
      title: 'Check emails/',
      description: 'If an account exists for that email, a reset code was written to emails/ on the API host.',
      color: 'success'
    })
    await navigateTo({
      path: '/reset-password',
      query: { email: value }
    })
  } catch (error) {
    errorMessage.value = problemMessage(error, 'Unable to send reset email.')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">
        Forgot password
      </h1>
      <p class="text-sm text-muted">
        Enter the email for your account. The reset code is written to emails/ on the API host.
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
          v-model="email"
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
        Send reset code
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
