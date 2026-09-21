<script setup lang="ts">
const props = defineProps<{
  codes: string[]
}>()

const { copy, download } = useRecoveryCodes()
</script>

<template>
  <UCard>
    <template #header>
      <div>
        <p class="font-semibold">
          Keep these codes in a safe place
        </p>
        <p class="text-sm text-muted">
          Each code can be used once. They will not be shown again.
        </p>
      </div>
    </template>
    <ul class="space-y-1 font-mono text-sm">
      <li
        v-for="value in props.codes"
        :key="value"
      >
        {{ value }}
      </li>
    </ul>
    <template #footer>
      <div class="flex gap-2">
        <UButton
          label="Copy"
          @click="copy(props.codes)"
        />
        <UButton
          label="Download as txt"
          color="neutral"
          variant="outline"
          @click="download(props.codes)"
        />
      </div>
    </template>
  </UCard>
</template>
