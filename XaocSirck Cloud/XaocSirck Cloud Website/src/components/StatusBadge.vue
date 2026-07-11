<script setup lang="ts">
defineProps<{
  status: 'running' | 'pending' | 'success' | 'error' | 'warning' | 'idle'
  label?: string
}>()

const statusConfig: Record<string, { bg: string; color: string; dot: string; defaultLabel: string }> = {
  running: { bg: 'rgba(0, 122, 255, 0.1)', color: '#004fad', dot: '#007aff', defaultLabel: '运行中' },
  pending: { bg: 'rgba(255, 159, 10, 0.1)', color: '#c2750a', dot: '#ff9f0a', defaultLabel: '等待中' },
  success: { bg: 'rgba(52, 199, 89, 0.1)', color: '#1a7d34', dot: '#34c759', defaultLabel: '已完成' },
  error: { bg: 'rgba(255, 59, 48, 0.1)', color: '#c22b22', dot: '#ff3b30', defaultLabel: '失败' },
  warning: { bg: 'rgba(255, 159, 10, 0.12)', color: '#946000', dot: '#ff9f0a', defaultLabel: '告警' },
  idle: { bg: 'rgba(142, 142, 147, 0.1)', color: '#6e6e73', dot: '#8e8e93', defaultLabel: '空闲' },
}
</script>

<template>
  <span class="status-badge" :style="{ background: statusConfig[status]!.bg, color: statusConfig[status]!.color }">
    <span class="status-dot" :style="{ background: statusConfig[status]!.dot }"></span>
    {{ label || statusConfig[status]!.defaultLabel }}
  </span>
</template>

<style scoped>
.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.3125rem;
  padding: 0.1875rem 0.5625rem;
  border-radius: 9999px;
  font-size: 0.6875rem;
  font-weight: 500;
  white-space: nowrap;
  font-family: 'JetBrains Mono', monospace;
}

.status-dot {
  width: 0.375rem;
  height: 0.375rem;
  border-radius: 50%;
  flex-shrink: 0;
}
</style>
