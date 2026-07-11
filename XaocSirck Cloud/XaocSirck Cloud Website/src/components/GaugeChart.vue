<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  value: number
  label: string
  unit?: string
  color?: 'blue' | 'green' | 'orange' | 'red'
}>()

const colorMap = {
  blue: '#007aff',
  green: '#34c759',
  orange: '#ff9f0a',
  red: '#ff3b30',
}

const radius = 52
const circumference = 2 * Math.PI * radius
const strokeOffset = computed(() => circumference - (props.value / 100) * circumference)
const arcColor = computed(() => colorMap[props.color || 'blue'])
</script>

<template>
  <div class="gauge-card">
    <div class="gauge-ring">
      <svg width="128" height="128" viewBox="0 0 128 128">
        <circle cx="64" cy="64" :r="radius" fill="none" stroke="rgba(0,0,0,0.05)" stroke-width="8" />
        <circle
          cx="64" cy="64" :r="radius" fill="none"
          :stroke="arcColor" stroke-width="8" stroke-linecap="round"
          :stroke-dasharray="circumference"
          :stroke-dashoffset="strokeOffset"
          transform="rotate(-90 64 64)"
          style="transition: stroke-dashoffset 600ms cubic-bezier(0.32, 0.72, 0, 1);"
        />
      </svg>
      <div class="gauge-center">
        <span class="gauge-value">{{ value }}<span class="gauge-unit">{{ unit || '%' }}</span></span>
      </div>
    </div>
    <span class="gauge-label">{{ label }}</span>
  </div>
</template>

<style scoped>
.gauge-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
  padding: 1.5rem;
  background: rgba(255, 255, 255, 0.6);
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border: 1px solid rgba(0, 0, 0, 0.06);
  border-radius: 1.25rem;
}

.gauge-ring {
  position: relative;
  width: 128px;
  height: 128px;
}

.gauge-center {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
}

.gauge-value {
  font-size: 1.75rem;
  font-weight: 700;
  color: #1d1d1f;
  font-family: 'JetBrains Mono', monospace;
  letter-spacing: -0.02em;
}

.gauge-unit {
  font-size: 0.875rem;
  font-weight: 500;
  color: rgba(60, 60, 67, 0.5);
  margin-left: 0.125rem;
}

.gauge-label {
  font-size: 0.8125rem;
  font-weight: 500;
  color: rgba(60, 60, 67, 0.6);
}
</style>
