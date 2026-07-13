<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()

interface NavItem {
  label: string
  sublabel?: string
  icon: string
  to?: string
  children?: { label: string; sublabel?: string; to: string }[]
}

interface NavGroup {
  title: string
  items: NavItem[]
}

const groups: NavGroup[] = [
  {
    title: '概览',
    items: [
      { label: '管理中心', sublabel: '系统总览', icon: 'dashboard', to: '/' },
    ],
  },
  {
    title: '主系统',
    items: [
      { label: '错误中心', sublabel: '告警日志与回调', icon: 'alert', to: '/system/errors' },
      { label: '系统健康', sublabel: 'CPU / RAM / 流量', icon: 'activity', to: '/system/health' },
      { label: '安全 / IP 管理', sublabel: 'IP 统计与黑名单', icon: 'shield', to: '/system/security' },
    ],
  },
  {
    title: '缓存系统',
    items: [
      {
        label: '缓存查询',
        icon: 'search',
        to: '/cache/query',
      },
      {
        label: '热缓存更新',
        icon: 'upload',
        children: [
          { label: '构建 / 删除', sublabel: '强制构建 / 清除热缓存', to: '/cache/hot' },
        ],
      },
      {
        label: '冷缓存管理',
        icon: 'database',
        children: [
          { label: '条目管理', sublabel: '增删 / 分页 / 清空', to: '/cache/cold' },
        ],
      },
      {
        label: '服务控制',
        sublabel: '暂停 / 重启',
        icon: 'power',
        to: '/cache/service',
      },
      {
        label: '更新流程',
        sublabel: '维护向导',
        icon: 'flow',
        to: '/cache/flow',
      },
      {
        label: '签名云',
        sublabel: '签名录入与查询',
        icon: 'shield',
        to: '/signature',
      },
    ],
  },
  {
    title: '文档',
    items: [
      { label: 'API 文档', sublabel: '缓存服务接口', icon: 'book', to: '/docs/api' },
    ],
  },
  {
    title: '配置',
    items: [
      { label: 'Token 配置', sublabel: 'JWT 鉴权凭证', icon: 'key', to: '/settings/token' },
    ],
  },
]

const expandedMenus = ref<Record<string, boolean>>({
  '热缓存更新': true,
  '冷缓存管理': true,
})

function toggleMenu(label: string) {
  expandedMenus.value[label] = !expandedMenus.value[label]
}

function isChildActive(to: string): boolean {
  return route.path === to
}
</script>

<template>
  <aside class="sidebar">
    <div class="sidebar-header">
      <div class="brand">
        <div class="brand-logo">
          <img src="/logo.ico" alt="XaocSirck Cloud" width="22" height="22" />
        </div>
        <div class="brand-text">
          <span class="brand-name">XaocSirck Cloud</span>
          <span class="brand-sub">缓存扫描管理平台</span>
        </div>
      </div>
    </div>

    <div class="sidebar-separator" />

    <nav class="sidebar-nav">
      <div v-for="group in groups" :key="group.title" class="nav-group">
        <div class="nav-group-label">{{ group.title }}</div>
        <ul class="nav-list">
          <li v-for="item in group.items" :key="item.label" class="nav-item">
            <RouterLink
              v-if="item.to && !item.children"
              :to="item.to"
              class="nav-link"
              :class="{ active: route.path === item.to }"
            >
              <span class="nav-icon" v-html="icons[item.icon]"></span>
              <div class="nav-text">
                <span class="nav-label">{{ item.label }}</span>
                <span v-if="item.sublabel" class="nav-sublabel">{{ item.sublabel }}</span>
              </div>
            </RouterLink>
            <template v-else>
              <button
                class="nav-link nav-toggle"
                @click="toggleMenu(item.label)"
              >
                <span class="nav-icon" v-html="icons[item.icon]"></span>
                <div class="nav-text">
                  <span class="nav-label">{{ item.label }}</span>
                </div>
                <svg
                  class="chevron"
                  :class="{ rotated: expandedMenus[item.label] }"
                  width="14" height="14" viewBox="0 0 24 24" fill="none"
                  stroke="currentColor" stroke-width="2.5"
                  stroke-linecap="round" stroke-linejoin="round"
                >
                  <path d="m9 18 6-6-6-6" />
                </svg>
              </button>
              <ul v-if="expandedMenus[item.label]" class="nav-children">
                <li v-for="child in item.children" :key="child.label">
                  <RouterLink :to="child.to" class="nav-child-link" :class="{ active: isChildActive(child.to) }">
                    <div class="nav-text">
                      <span class="nav-label">{{ child.label }}</span>
                      <span v-if="child.sublabel" class="nav-sublabel">{{ child.sublabel }}</span>
                    </div>
                  </RouterLink>
                </li>
              </ul>
            </template>
          </li>
        </ul>
      </div>
    </nav>

    <div class="sidebar-footer">
      <div class="footer-user">
        <div class="user-avatar">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="12" cy="8" r="5" /><path d="M20 21a8 8 0 0 0-16 0" />
          </svg>
        </div>
        <div class="user-text">
          <span class="user-name">admin@xaocsirck.cloud</span>
          <span class="user-role">管理员 · x9k2</span>
        </div>
      </div>
    </div>
  </aside>
</template>

<script lang="ts">
const iconPaths: Record<string, string> = {
  dashboard: '<rect width="7" height="9" x="3" y="3" rx="1"/><rect width="7" height="5" x="14" y="3" rx="1"/><rect width="7" height="9" x="14" y="12" rx="1"/><rect width="7" height="5" x="3" y="16" rx="1"/>',
  alert: '<path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"/><path d="M12 9v4"/><path d="M12 17h.01"/>',
  activity: '<path d="M22 12h-2.48a2 2 0 0 0-1.93 1.46l-2.35 8.36a.25.25 0 0 1-.48 0L9.24 2.18a.25.25 0 0 0-.48 0l-2.35 8.36A2 2 0 0 1 4.49 12H2"/>',
  shield: '<path d="M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"/>',
  search: '<circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>',
  upload: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" x2="12" y1="3" y2="15"/>',
  database: '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5V19A9 3 0 0 0 21 19V5"/><path d="M3 12A9 3 0 0 0 21 12"/>',
  power: '<path d="M12 2v10"/><path d="M18.4 6.6a9 9 0 1 1-12.77.04"/>',
  flow: '<rect width="8" height="8" x="3" y="3" rx="2"/><path d="M7 11v4a2 2 0 0 0 2 2h4"/><rect width="8" height="8" x="13" y="13" rx="2"/>',
  book: '<path d="M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H20v20H6.5a2.5 2.5 0 0 1 0-5H20"/>',
  key: '<path d="M15.5 7.5 14 9l-7.5 7.5a2.121 2.121 0 1 0 3 3L17 12l1.5 1.5"/><circle cx="9" cy="15" r="1"/><path d="M21 11l-3.5-3.5a2.121 2.121 0 0 0-3 0L11 11"/>',
  server: '<rect width="20" height="8" x="2" y="2" rx="2"/><rect width="20" height="8" x="2" y="14" rx="2"/><line x1="6" x2="6.01" y1="6" y2="6"/><line x1="6" x2="6.01" y1="18" y2="18"/>',
  info: '<circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/>',
}

export const icons = iconPaths
</script>

<style scoped>
.sidebar {
  width: 16rem;
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: rgba(242, 242, 247, 0.72);
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border-right: 1px solid rgba(0, 0, 0, 0.06);
  flex-shrink: 0;
  position: sticky;
  top: 0;
}

.sidebar-header {
  padding: 0.75rem;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  padding: 0.5rem;
  border-radius: 0.625rem;
  height: 3rem;
}

.brand-logo {
  width: 2.5rem;
  height: 2.5rem;
  border-radius: 0.625rem;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  overflow: hidden;
}

.brand-logo img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.brand-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
  line-height: 1.2;
}

.brand-name {
  font-size: 0.875rem;
  font-weight: 600;
  color: #1d1d1f;
  white-space: nowrap;
}

.brand-sub {
  font-size: 0.75rem;
  color: rgba(60, 60, 67, 0.6);
  white-space: nowrap;
}

.sidebar-separator {
  height: 1px;
  margin: 0 0.5rem;
  background: rgba(0, 0, 0, 0.06);
}

.sidebar-nav {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
  padding: 0.5rem;
  scrollbar-width: none;
}

.sidebar-nav::-webkit-scrollbar {
  display: none;
}

.nav-group {
  padding: 0.25rem 0;
}

.nav-group-label {
  font-size: 0.6875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: rgba(60, 60, 67, 0.5);
  padding: 0.5rem 0.5rem 0.25rem;
}

.nav-list {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.nav-item {
  position: relative;
}

.nav-link {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.5rem;
  border-radius: 0.5rem;
  text-align: left;
  background: transparent;
  border: none;
  cursor: pointer;
  transition: background 150ms ease, color 150ms ease;
  min-height: 2.25rem;
  font-size: 0.8125rem;
  color: #1d1d1f;
  font-family: inherit;
  text-decoration: none;
}

.nav-link:hover {
  background: rgba(0, 0, 0, 0.04);
}

.nav-link.active {
  background: rgba(0, 122, 255, 0.1);
  font-weight: 500;
}

.nav-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
  color: rgba(60, 60, 67, 0.6);
}

.nav-link.active .nav-icon {
  color: #007aff;
}

.nav-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
  line-height: 1.25;
}

.nav-label {
  font-size: 0.8125rem;
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: #1d1d1f;
}

.nav-link:not(.active) .nav-label {
  color: rgba(60, 60, 67, 0.85);
}

.nav-sublabel {
  font-size: 0.6875rem;
  color: rgba(60, 60, 67, 0.5);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.chevron {
  flex-shrink: 0;
  color: rgba(60, 60, 67, 0.4);
  transition: transform 200ms ease;
}

.chevron.rotated {
  transform: rotate(90deg);
}

.nav-children {
  list-style: none;
  margin-left: 0.5rem;
  border-left: 1px solid rgba(0, 0, 0, 0.08);
  padding-left: 0.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.nav-child-link {
  display: flex;
  align-items: center;
  padding: 0.5rem;
  border-radius: 0.5rem;
  min-height: 2rem;
  cursor: pointer;
  transition: background 150ms ease;
  text-decoration: none;
}

.nav-child-link:hover {
  background: rgba(0, 0, 0, 0.04);
}

.nav-child-link.active .nav-label {
  color: #007aff;
}

.nav-child-link .nav-label {
  font-size: 0.8125rem;
  font-weight: 500;
  color: rgba(60, 60, 67, 0.7);
}

.sidebar-footer {
  padding: 0.5rem;
  flex-shrink: 0;
}

.footer-user {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  padding: 0.5rem;
  border-radius: 0.625rem;
  height: 3rem;
}

.user-avatar {
  width: 2rem;
  height: 2rem;
  border-radius: 0.5rem;
  background: rgba(0, 0, 0, 0.06);
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(60, 60, 67, 0.7);
  flex-shrink: 0;
}

.user-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
  line-height: 1.2;
}

.user-name {
  font-size: 0.8125rem;
  font-weight: 500;
  color: #1d1d1f;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.user-role {
  font-size: 0.6875rem;
  color: rgba(60, 60, 67, 0.55);
  white-space: nowrap;
}
</style>
