<script setup lang="ts">
const { open, finish } = useStepUpUi()
</script>

<template>
  <StepUpModal
    v-if="open"
    @close="finish"
  />
</template>
