<script setup lang="ts">
const props = withDefaults(defineProps<{
  icon: string
  title: string
  description?: string
  expandable?: boolean
  to?: string
  manageLabel?: string
}>(), {
  expandable: false,
  manageLabel: 'Manage'
})

const open = defineModel<boolean>('open', { default: false })

function toggle() {
  if (!props.expandable || props.to) {
    return
  }
  open.value = !open.value
}
</script>

<template>
  <div>
    <div
      class="flex justify-between gap-3 py-3"
      :class="props.expandable && !props.to ? 'cursor-pointer' : undefined"
      :aria-expanded="props.expandable ? open : undefined"
      @click="toggle"
    >
      <div class="flex min-w-0 gap-3">
        <div class="bg-elevated flex size-10 shrink-0 items-center justify-center rounded-full">
          <UIcon
            :name="props.icon"
            class="size-5"
          />
        </div>
        <div class="min-w-0">
          <p class="text-highlighted font-medium">
            {{ props.title }}
            <slot name="badge" />
          </p>
          <p class="mt-1 block pr-0 text-xs text-muted sm:pr-4">
            <slot name="summary">
              {{ props.description }}
            </slot>
          </p>
        </div>
      </div>
      <div
        class="flex shrink-0 items-center gap-2"
        @click.stop
      >
        <slot name="actions" />
        <UButton
          v-if="props.to"
          :to="props.to"
          color="neutral"
          variant="subtle"
          :label="props.manageLabel"
          trailing-icon="i-lucide-chevron-right"
        />
        <UButton
          v-else-if="props.expandable"
          color="neutral"
          variant="subtle"
          :label="props.manageLabel"
          :trailing-icon="open ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'"
          @click="toggle"
        />
      </div>
    </div>

    <div
      v-if="props.expandable && !props.to && open"
      class="pb-4"
    >
      <slot />
    </div>
  </div>
</template>
