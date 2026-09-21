<script setup lang="ts">
const { email } = useAuthState()
const { info, signOutCookie } = useAuth()
const busy = ref(false)

async function logout() {
  busy.value = true
  try {
    await signOutCookie()
    await navigateTo('/login')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="flex min-h-screen flex-col bg-muted/30">
    <header class="sticky top-0 z-20 border-b border-default bg-default/90 backdrop-blur">
      <div class="mx-auto flex max-w-3xl items-center justify-between gap-3 px-4 py-3">
        <NuxtLink
          to="/"
          class="text-sm font-semibold tracking-tight"
        >
          Tom Admin
        </NuxtLink>
        <div class="flex items-center gap-2">
          <UButton
            to="/security"
            size="sm"
            color="neutral"
            variant="ghost"
            icon="i-lucide-shield"
          >
            Security
          </UButton>
          <UButton
            to="/passkeys"
            size="sm"
            color="neutral"
            variant="ghost"
            icon="i-lucide-fingerprint"
          >
            Passkeys
          </UButton>
          <UBadge
            v-if="email || info?.email"
            color="success"
            variant="subtle"
          >
            {{ email || info?.email }}
          </UBadge>
          <UButton
            size="sm"
            color="neutral"
            variant="outline"
            icon="i-lucide-log-out"
            :loading="busy"
            @click="logout"
          >
            Logout
          </UButton>
          <UColorModeButton />
        </div>
      </div>
    </header>

    <main class="mx-auto w-full max-w-3xl flex-1 px-4 py-8">
      <slot />
    </main>
  </div>
</template>
