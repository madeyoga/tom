<script setup lang="ts">
const props = defineProps<{
  value: string
}>()

const dataUrl = ref('')

async function render() {
  if (!import.meta.client || !props.value) {
    dataUrl.value = ''
    return
  }
  const { toDataURL } = await import('qrcode')
  dataUrl.value = await toDataURL(props.value, {
    width: 192,
    margin: 1,
    errorCorrectionLevel: 'M'
  })
}

watch(() => props.value, render, { immediate: true })
</script>

<template>
  <img
    v-if="dataUrl"
    :src="dataUrl"
    alt="Authenticator QR code"
    class="size-48 rounded-md bg-white p-2"
  >
</template>
