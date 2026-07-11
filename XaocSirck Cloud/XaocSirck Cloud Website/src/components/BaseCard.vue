<script setup lang="ts">
defineProps<{
  title: string
  desc?: string
  iconColor?: 'blue' | 'green' | 'orange' | 'red' | 'neutral' | 'purple'
}>()
</script>

<template>
  <div class="base-card">
    <div v-if="title || $slots.header" class="card-header">
      <div v-if="title" class="card-title">
        <slot name="icon" />
        {{ title }}
      </div>
      <p v-if="desc" class="card-desc">{{ desc }}</p>
      <slot name="toolbar" />
    </div>
    <div class="card-body">
      <slot />
    </div>
  </div>
</template>

<style scoped>
.base-card {
  background: rgba(255, 255, 255, 0.6);
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border: 1px solid rgba(0, 0, 0, 0.06);
  border-radius: 1.25rem;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.card-header {
  padding: 1.25rem 1.5rem 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.card-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9375rem;
  font-weight: 600;
  color: #1d1d1f;
}

.card-desc {
  font-size: 0.8125rem;
  color: rgba(60, 60, 67, 0.55);
}

.card-body {
  padding: 0 1.5rem 1.5rem;
}
</style>
