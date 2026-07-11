<script setup lang="ts"></script>

<template>
  <footer class="app-footer">
    <div class="footer-left">
      <span class="footer-brand">© 2026 XaocSirck Cloud</span>
      <span class="footer-divider">·</span>
      <span class="footer-meta">v3.2.1 · Build 20260710</span>
      <span class="footer-divider">·</span>
      <a class="footer-link">服务条款</a>
      <a class="footer-link">隐私政策</a>
    </div>
    <div class="footer-right">
      <span class="footer-status">
        <span class="status-pulse"></span>
        所有区域服务正常
      </span>
      <span class="footer-divider">·</span>
      <span class="footer-meta">延迟 12ms</span>
    </div>
  </footer>
</template>

<style scoped>
.app-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.875rem 1.5rem;
  font-size: 0.75rem;
  color: rgba(60, 60, 67, 0.5);
  flex-shrink: 0;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.footer-left {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.footer-right {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.footer-brand {
  font-weight: 600;
  color: rgba(60, 60, 67, 0.7);
}

.footer-divider {
  color: rgba(60, 60, 67, 0.3);
}

.footer-meta {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.6875rem;
}

.footer-link {
  color: rgba(60, 60, 67, 0.55);
  cursor: pointer;
  transition: color 150ms ease;
}

.footer-link:hover {
  color: #007aff;
}

.footer-status {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  color: #1a7d34;
  font-weight: 500;
}

.status-pulse {
  width: 0.5rem;
  height: 0.5rem;
  border-radius: 50%;
  background: #34c759;
  position: relative;
}

.status-pulse::after {
  content: '';
  position: absolute;
  inset: -2px;
  border-radius: 50%;
  background: #34c759;
  opacity: 0.3;
  animation: pulse 2s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { transform: scale(1); opacity: 0.3; }
  50% { transform: scale(1.6); opacity: 0; }
}
</style>
