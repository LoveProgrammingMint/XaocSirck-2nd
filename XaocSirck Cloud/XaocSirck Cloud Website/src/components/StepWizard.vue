<script setup lang="ts">
defineProps<{
  steps: { title: string; desc: string; api?: string; method?: string }[]
  current: number
}>()

defineEmits<{
  'update:current': [value: number]
}>()
</script>

<template>
  <div class="wizard">
    <div class="wizard-track">
      <div
        v-for="(step, i) in steps"
        :key="i"
        class="wizard-step"
        :class="{
          active: i === current,
          done: i < current,
        }"
      >
        <button class="step-node" @click="$emit('update:current', i)">
          <svg v-if="i < current" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
            <path d="M20 6 9 17l-5-5" />
          </svg>
          <span v-else>{{ i + 1 }}</span>
        </button>
        <div class="step-info">
          <span class="step-title">{{ step.title }}</span>
          <span class="step-desc">{{ step.desc }}</span>
          <span v-if="step.api" class="step-api">
            <span class="api-method" :class="step.method?.toLowerCase()">{{ step.method }}</span>
            {{ step.api }}
          </span>
        </div>
        <div v-if="i < steps.length - 1" class="step-connector" :class="{ filled: i < current }"></div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.wizard {
  padding: 0.5rem 0;
}

.wizard-track {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.wizard-step {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  position: relative;
  padding-bottom: 2rem;
}

.wizard-step:last-child {
  padding-bottom: 0;
}

.step-node {
  width: 2rem;
  height: 2rem;
  border-radius: 50%;
  border: 2px solid rgba(0, 0, 0, 0.1);
  background: rgba(255, 255, 255, 0.8);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.8125rem;
  font-weight: 600;
  font-family: 'JetBrains Mono', monospace;
  color: rgba(60, 60, 67, 0.5);
  cursor: pointer;
  transition: all 200ms ease;
  flex-shrink: 0;
  z-index: 1;
}

.wizard-step.active .step-node {
  border-color: #007aff;
  background: #007aff;
  color: #ffffff;
  box-shadow: 0 0 0 4px rgba(0, 122, 255, 0.12);
}

.wizard-step.done .step-node {
  border-color: #34c759;
  background: #34c759;
  color: #ffffff;
}

.step-info {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
  padding-top: 0.125rem;
  flex: 1;
}

.step-title {
  font-size: 0.875rem;
  font-weight: 600;
  color: #1d1d1f;
}

.wizard-step:not(.active):not(.done) .step-title {
  color: rgba(60, 60, 67, 0.6);
}

.step-desc {
  font-size: 0.75rem;
  color: rgba(60, 60, 67, 0.5);
  line-height: 1.4;
}

.step-api {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.6875rem;
  font-family: 'JetBrains Mono', monospace;
  color: rgba(60, 60, 67, 0.5);
  margin-top: 0.25rem;
}

.api-method {
  padding: 0.0625rem 0.375rem;
  border-radius: 0.25rem;
  font-weight: 600;
  font-size: 0.625rem;
}

.api-method.get { background: rgba(52, 199, 89, 0.12); color: #1a7d34; }
.api-method.post { background: rgba(0, 122, 255, 0.12); color: #004fad; }

.step-connector {
  position: absolute;
  left: 1rem;
  top: 2rem;
  bottom: 0;
  width: 2px;
  background: rgba(0, 0, 0, 0.08);
  z-index: 0;
}

.step-connector.filled {
  background: #34c759;
}
</style>
