<script setup lang="ts">
const open = defineModel<boolean>('open', { default: false })

defineProps<{
  codes: string[]
}>()

const emit = defineEmits<{
  close: []
}>()

function onOpenChange(value: boolean) {
  open.value = value
  if (!value) {
    emit('close')
  }
}
</script>

<template>
  <UModal
    :open="open"
    title="Recovery codes"
    description="Store these codes now. Old codes no longer work, and these will not be shown again."
    :dismissible="false"
    @update:open="onOpenChange"
  >
    <template #body>
      <SettingsRecoveryCodesPanel :codes="codes" />
    </template>
    <template #footer>
      <div class="flex justify-end">
        <UButton
          label="Done"
          @click="onOpenChange(false)"
        />
      </div>
    </template>
  </UModal>
</template>
