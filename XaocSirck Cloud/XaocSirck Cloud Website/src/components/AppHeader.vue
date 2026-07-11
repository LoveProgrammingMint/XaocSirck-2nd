<script setup lang="ts">
defineProps<{
  title: string
  subtitle?: string
}>()
</script>

<template>
  <header class="app-header">
    <div class="header-left">
      <button class="sidebar-toggle">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <rect width="18" height="18" x="3" y="3" rx="2" />
          <path d="M9 3v18" />
        </svg>
      </button>
      <div class="header-divider" />
      <div class="header-title-block">
        <div class="header-title">{{ title }}</div>
        <div v-if="subtitle" class="header-subtitle">{{ subtitle }}</div>
      </div>
    </div>
    <div class="header-right">
      <div class="search-box">
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="11" cy="11" r="8" />
          <path d="m21 21-4.3-4.3" />
        </svg>
        <input type="text" placeholder="搜索资源、实例、日志…" />
        <kbd>⌘K</kbd>
      </div>
      <button class="icon-btn">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" />
          <path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" />
        </svg>
        <span class="badge-dot"></span>
      </button>
    </div>
  </header>
</template>

<style scoped>
.app-header {
  position: sticky;
  top: 0;
  z-index: 20;
  height: 4rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 1.5rem;
  background: rgba(255, 255, 255, 0.72);
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border-bottom: 1px solid rgba(0, 0, 0, 0.06);
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
}

.sidebar-toggle {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 1.75rem;
  height: 1.75rem;
  border-radius: 0.5rem;
  border: none;
  background: transparent;
  color: rgba(60, 60, 67, 0.7);
  cursor: pointer;
  transition: background 150ms ease;
}

.sidebar-toggle:hover {
  background: rgba(0, 0, 0, 0.05);
}

.header-divider {
  width: 1px;
  height: 1rem;
  background: rgba(0, 0, 0, 0.1);
  margin: 0 0.25rem;
}

.header-title-block {
  display: flex;
  flex-direction: column;
  min-width: 0;
  line-height: 1.2;
}

.header-title {
  font-size: 0.875rem;
  font-weight: 600;
  color: #1d1d1f;
  white-space: nowrap;
}

.header-subtitle {
  font-size: 0.75rem;
  color: rgba(60, 60, 67, 0.55);
  white-space: nowrap;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.search-box {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0 0.75rem;
  height: 2.25rem;
  width: 18rem;
  border-radius: 0.625rem;
  background: rgba(0, 0, 0, 0.04);
  border: 1px solid transparent;
  transition: all 150ms ease;
  color: rgba(60, 60, 67, 0.5);
}

.search-box:focus-within {
  background: rgba(255, 255, 255, 0.9);
  border-color: #007aff;
  box-shadow: 0 0 0 3px rgba(0, 122, 255, 0.12);
  color: #007aff;
}

.search-box input {
  flex: 1;
  border: none;
  background: transparent;
  outline: none;
  font-size: 0.8125rem;
  color: #1d1d1f;
  font-family: inherit;
}

.search-box input::placeholder {
  color: rgba(60, 60, 67, 0.45);
}

.search-box kbd {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.6875rem;
  padding: 0.125rem 0.375rem;
  border-radius: 0.3125rem;
  background: rgba(0, 0, 0, 0.06);
  color: rgba(60, 60, 67, 0.55);
  border: 1px solid rgba(0, 0, 0, 0.06);
}

.icon-btn {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.25rem;
  height: 2.25rem;
  border-radius: 0.625rem;
  border: 1px solid rgba(0, 0, 0, 0.06);
  background: rgba(255, 255, 255, 0.6);
  color: rgba(60, 60, 67, 0.7);
  cursor: pointer;
  transition: all 150ms ease;
}

.icon-btn:hover {
  background: rgba(255, 255, 255, 0.9);
  color: #007aff;
}

.badge-dot {
  position: absolute;
  top: 0.5rem;
  right: 0.5rem;
  width: 0.5rem;
  height: 0.5rem;
  border-radius: 50%;
  background: #ff3b30;
  border: 2px solid #ffffff;
}
</style>
