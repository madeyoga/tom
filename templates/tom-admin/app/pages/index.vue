<script setup lang="ts">
definePageMeta({
  layout: 'app',
  middleware: 'auth'
})

useSeoMeta({
  title: 'Home'
})

const { info, refreshCookieSession, signOutCookie } = useAuth()
const busy = ref(false)

const claims = computed(() => (info.value?.claims ?? []).slice(0, 8))

async function logout() {
  busy.value = true
  try {
    await signOutCookie()
    await navigateTo('/login')
  } finally {
    busy.value = false
  }
}

onMounted(async () => {
  const session = await refreshCookieSession()
  if (!session) {
    await navigateTo({
      path: '/login',
      query: { redirect: '/' }
    })
  }
})
</script>

<template>
  <section class="space-y-4">
    <div class="space-y-1">
      <h1 class="text-2xl font-semibold tracking-tight">
        Home
      </h1>
      <p class="text-sm text-muted">
        Signed in with a cookie session. This is a dummy dashboard for the stamped admin.
      </p>
    </div>

    <div class="flex flex-wrap gap-2">
      <UButton
        to="/security"
        icon="i-lucide-shield"
      >
        Security
      </UButton>
      <UButton
        to="/passkeys"
        color="neutral"
        variant="outline"
        icon="i-lucide-fingerprint"
      >
        Passkeys
      </UButton>
      <UButton
        color="neutral"
        variant="outline"
        icon="i-lucide-log-out"
        :loading="busy"
        @click="logout"
      >
        Logout
      </UButton>
    </div>

    <UCard>
      <dl class="space-y-3 text-sm">
        <div class="flex items-center justify-between gap-3">
          <dt class="text-muted">
            Email
          </dt>
          <dd class="font-medium">
            {{ info?.email || '—' }}
          </dd>
        </div>
        <div class="flex items-center justify-between gap-3">
          <dt class="text-muted">
            Email confirmed
          </dt>
          <dd>
            <UBadge
              :color="info?.isEmailConfirmed ? 'success' : 'warning'"
              variant="subtle"
            >
              {{ info?.isEmailConfirmed ? 'Yes' : 'No' }}
            </UBadge>
          </dd>
        </div>
      </dl>
    </UCard>

    <UCard v-if="claims.length">
      <template #header>
        <p class="text-sm font-semibold">
          Claims
        </p>
      </template>
      <ul class="space-y-2 font-mono text-xs">
        <li
          v-for="claim in claims"
          :key="`${claim.type}:${claim.value}`"
          class="flex justify-between gap-3"
        >
          <span class="truncate text-muted">{{ claim.type }}</span>
          <span class="truncate">{{ claim.value }}</span>
        </li>
      </ul>
    </UCard>
  </section>
</template>
