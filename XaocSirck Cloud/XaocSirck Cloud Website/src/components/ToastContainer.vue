<script setup lang="ts">
import { useToast } from '@/composables/useToast'

const { toasts } = useToast()

const iconMap = {
  success: '<path d="M20 6 9 17l-5-5"/>',
  error: '<circle cx="12" cy="12" r="10"/><path d="m15 9-6 6"/><path d="m9 9 6 6"/>',
  info: '<circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/>',
}

const colorMap = {
  success: { bg: 'rgba(52, 199, 89, 0.95)', border: 'rgba(52, 199, 89, 0.3)' },
  error: { bg: 'rgba(255, 59, 48, 0.95)', border: 'rgba(255, 59, 48, 0.3)' },
  info: { bg: 'rgba(0, 122, 255, 0.95)', border: 'rgba(0, 122, 255, 0.3)' },
}
</script>

<template>
  <TransitionGroup name="toast" tag="div" class="toast-container">
    <div
      v-for="t in toasts"
      :key="t.id"
      class="toast"
      :style="{ background: colorMap[t.type].bg, borderColor: colorMap[t.type].border }"
    >
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" v-html="iconMap[t.type]"></svg>
      <span>{{ t.message }}</span>
    </div>
  </TransitionGroup>
</template>

<style scoped>
.toast-container {
  position: fixed;
  top: 5rem;
  right: 1.5rem;
  z-index: 9999;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  pointer-events: none;
}

.toast {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.625rem 1rem;
  border-radius: 0.75rem;
  color: #ffffff;
  font-size: 0.8125rem;
  font-weight: 500;
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border: 1px solid;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.12);
  min-width: 16rem;
  pointer-events: auto;
}

.toast-enter-active,
.toast-leave-active {
  transition: all 300ms cubic-bezier(0.32, 0.72, 0, 1);
}

.toast-enter-from {
  opacity: 0;
  transform: translateX(2rem);
}

.toast-leave-to {
  opacity: 0;
  transform: translateX(2rem);
}
</style>
