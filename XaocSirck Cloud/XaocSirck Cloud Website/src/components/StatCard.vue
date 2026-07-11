<script setup lang="ts">
defineProps<{
  label: string
  value: string
  unit?: string
  trend?: number
  icon?: string
  accent?: 'blue' | 'green' | 'orange' | 'red' | 'neutral'
}>()

const accentColors: Record<string, { bg: string; fg: string; bar: string }> = {
  blue: { bg: 'rgba(0, 122, 255, 0.08)', fg: '#007aff', bar: 'linear-gradient(135deg, #007aff, #2e8dff)' },
  green: { bg: 'rgba(52, 199, 89, 0.08)', fg: '#34c759', bar: 'linear-gradient(135deg, #34c759, #30d158)' },
  orange: { bg: 'rgba(255, 159, 10, 0.08)', fg: '#ff9f0a', bar: 'linear-gradient(135deg, #ff9f0a, #ffb340)' },
  red: { bg: 'rgba(255, 59, 48, 0.08)', fg: '#ff3b30', bar: 'linear-gradient(135deg, #ff3b30, #ff6961)' },
  neutral: { bg: 'rgba(142, 142, 147, 0.08)', fg: '#6e6e73', bar: 'linear-gradient(135deg, #8e8e93, #aeaeb2)' },
}
</script>

<template>
  <div class="stat-card" :class="`accent-${accent || 'neutral'}`">
    <div class="stat-top">
      <div class="stat-icon" :style="{ background: accentColors[accent || 'neutral']!.bg, color: accentColors[accent || 'neutral']!.fg }">
        <svg v-if="icon === 'cpu'" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="16" height="16" x="4" y="4" rx="2"/><rect width="6" height="6" x="9" y="9" rx="1"/><path d="M15 2v2"/><path d="M15 20v2"/><path d="M2 15h2"/><path d="M2 9h2"/><path d="M20 15h2"/><path d="M20 9h2"/><path d="M9 2v2"/><path d="M9 20v2"/></svg>
        <svg v-else-if="icon === 'memory'" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 19v-3"/><path d="M10 19v-3"/><path d="M14 19v-3"/><path d="M18 19v-3"/><path d="M8 11V9"/><path d="M16 11V9"/><path d="M12 11V9"/><path d="M2 15h20"/><path d="M2 7a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v1.1a2 2 0 0 0 0 3.837V17a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-5.1a2 2 0 0 0 0-3.837Z"/></svg>
        <svg v-else-if="icon === 'storage'" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5V19A9 3 0 0 0 21 19V5"/><path d="M3 12A9 3 0 0 0 21 12"/></svg>
        <svg v-else-if="icon === 'network'" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 2v20"/><path d="M2 5h20"/><path d="M2 10h20"/><path d="M2 15h20"/><path d="M2 20h20"/></svg>
        <svg v-else-if="icon === 'activity'" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 12h-2.48a2 2 0 0 0-1.93 1.46l-2.35 8.36a.25.25 0 0 1-.48 0L9.24 2.18a.25.25 0 0 0-.48 0l-2.35 8.36A2 2 0 0 1 4.49 12H2"/></svg>
        <svg v-else-if="icon === 'server'" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="20" height="8" x="2" y="2" rx="2"/><rect width="20" height="8" x="2" y="14" rx="2"/><line x1="6" x2="6.01" y1="6" y2="6"/><line x1="6" x2="6.01" y1="18" y2="18"/></svg>
        <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/></svg>
      </div>
      <div v-if="trend !== undefined" class="stat-trend" :class="trend >= 0 ? 'up' : 'down'">
        <svg v-if="trend >= 0" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="m6 15 6-6 6 6"/></svg>
        <svg v-else width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg>
        {{ Math.abs(trend) }}%
      </div>
    </div>
    <div class="stat-value">
      <span class="stat-number">{{ value }}</span>
      <span v-if="unit" class="stat-unit">{{ unit }}</span>
    </div>
    <div class="stat-label">{{ label }}</div>
    <div class="stat-bar">
      <div class="stat-bar-fill" :style="{ background: accentColors[accent || 'neutral']!.bar }"></div>
    </div>
  </div>
</template>

<style scoped>
.stat-card {
  background: rgba(255, 255, 255, 0.6);
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border: 1px solid rgba(0, 0, 0, 0.06);
  border-radius: 1.25rem;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  transition: transform 200ms cubic-bezier(0.32, 0.72, 0, 1), box-shadow 200ms ease;
  position: relative;
  overflow: hidden;
}

.stat-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  opacity: 0.8;
}

.stat-card.accent-blue::before { background: linear-gradient(90deg, #007aff, #2e8dff); }
.stat-card.accent-green::before { background: linear-gradient(90deg, #34c759, #30d158); }
.stat-card.accent-orange::before { background: linear-gradient(90deg, #ff9f0a, #ffb340); }
.stat-card.accent-red::before { background: linear-gradient(90deg, #ff3b30, #ff6961); }
.stat-card.accent-neutral::before { background: linear-gradient(90deg, #8e8e93, #aeaeb2); }

.stat-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 24px -8px rgba(0, 0, 0, 0.08), 0 4px 8px -4px rgba(0, 0, 0, 0.05);
}

.stat-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.stat-icon {
  width: 2.25rem;
  height: 2.25rem;
  border-radius: 0.625rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.stat-trend {
  display: flex;
  align-items: center;
  gap: 0.125rem;
  font-size: 0.6875rem;
  font-weight: 600;
  font-family: 'JetBrains Mono', monospace;
}

.stat-trend.up { color: #1a7d34; }
.stat-trend.down { color: #c22b22; }

.stat-value {
  display: flex;
  align-items: baseline;
  gap: 0.25rem;
}

.stat-number {
  font-size: 1.75rem;
  font-weight: 700;
  color: #1d1d1f;
  letter-spacing: -0.02em;
  font-family: 'JetBrains Mono', monospace;
  line-height: 1.1;
}

.stat-unit {
  font-size: 0.75rem;
  color: rgba(60, 60, 67, 0.5);
  font-weight: 500;
}

.stat-label {
  font-size: 0.75rem;
  color: rgba(60, 60, 67, 0.6);
  font-weight: 500;
}

.stat-bar {
  height: 0.25rem;
  border-radius: 9999px;
  background: rgba(0, 0, 0, 0.04);
  overflow: hidden;
  margin-top: 0.25rem;
}

.stat-bar-fill {
  height: 100%;
  border-radius: 9999px;
  width: 65%;
  opacity: 0.7;
}
</style>
