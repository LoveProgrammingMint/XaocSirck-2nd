<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  data: number[]
  labels?: string[]
  color?: 'blue' | 'green' | 'orange' | 'red' | 'purple'
  height?: number
  unit?: string
}>()

const colorMap = {
  blue: { bar: '#007aff', glow: 'rgba(0, 122, 255, 0.15)' },
  green: { bar: '#34c759', glow: 'rgba(52, 199, 89, 0.15)' },
  orange: { bar: '#ff9f0a', glow: 'rgba(255, 159, 10, 0.15)' },
  red: { bar: '#ff3b30', glow: 'rgba(255, 59, 48, 0.15)' },
  purple: { bar: '#5e5ce6', glow: 'rgba(94, 92, 230, 0.15)' },
}

const maxVal = computed(() => Math.max(...props.data, 1))
const barColor = computed(() => colorMap[props.color || 'blue'].bar)
const glowColor = computed(() => colorMap[props.color || 'blue'].glow)
</script>

<template>
  <div class="histogram" :style="{ height: (height || 160) + 'px' }">
    <div class="bars">
      <div
        v-for="(val, i) in data"
        :key="i"
        class="bar-wrapper"
        :title="`${labels?.[i] || ''}: ${val}${unit || ''}`"
      >
        <div class="bar-track">
          <div
            class="bar-fill"
            :style="{
              height: (val / maxVal * 100) + '%',
              background: `linear-gradient(180deg, ${barColor}, ${barColor}cc)`,
              boxShadow: `0 0 8px ${glowColor}`,
            }"
          ></div>
        </div>
        <span v-if="labels && labels[i]" class="bar-label">{{ labels[i] }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.histogram {
  width: 100%;
  display: flex;
  flex-direction: column;
}

.bars {
  display: flex;
  align-items: flex-end;
  gap: 2px;
  height: 100%;
  padding-bottom: 1.25rem;
  position: relative;
}

.bar-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  height: 100%;
  min-width: 0;
  position: relative;
}

.bar-track {
  flex: 1;
  width: 100%;
  display: flex;
  align-items: flex-end;
  justify-content: center;
  min-height: 2px;
}

.bar-fill {
  width: 100%;
  max-width: 24px;
  border-radius: 3px 3px 0 0;
  transition: height 400ms cubic-bezier(0.32, 0.72, 0, 1);
  min-height: 2px;
}

.bar-label {
  position: absolute;
  bottom: 0;
  font-size: 0.5625rem;
  font-family: 'JetBrains Mono', monospace;
  color: rgba(60, 60, 67, 0.4);
  white-space: nowrap;
}
</style>
