<script setup lang="ts">
import { useRouter } from 'vue-router'
import { onMounted, ref } from 'vue'
import StatCard from '@/components/StatCard.vue'
import BaseCard from '@/components/BaseCard.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { useToast } from '@/composables/useToast'
import { getStats } from '@/api/system'
import type { Stats } from '@/api/system'

const router = useRouter()
const toast = useToast()

const stats = ref<Stats>({
  total_requests: 0,
  requests_1h: 0,
  requests_1d: 0,
  avg_duration_ms: 0,
  peak_duration_ms: 0,
  avg_response_size: 0,
})
const loading = ref(false)

async function loadStats() {
  loading.value = true
  try {
    stats.value = await getStats()
  } catch (e) {
    toast.error('加载统计数据失败：' + (e instanceof Error ? e.message : '未知错误'))
  } finally {
    loading.value = false
  }
}

onMounted(loadStats)

const quickLinks = [
  { title: '缓存查询', desc: 'SHA256 哈希查询', icon: 'search', to: '/cache/query', color: 'blue' },
  { title: '热缓存更新', desc: '上传 MPHF 文件', icon: 'upload', to: '/cache/hot', color: 'orange' },
  { title: '冷缓存管理', desc: '条目增删与分页', icon: 'database', to: '/cache/cold', color: 'green' },
  { title: '服务控制', desc: '暂停 / 重启', icon: 'power', to: '/cache/service', color: 'red' },
  { title: '错误中心', desc: '告警日志', icon: 'alert', to: '/system/errors', color: 'orange' },
  { title: '系统健康', desc: 'CPU / RAM / 流量', icon: 'activity', to: '/system/health', color: 'blue' },
]

const systemStatus = [
  { name: '缓存查询服务', status: 'running' as const, detail: '响应正常 · 2.3ms avg' },
  { name: 'PostgreSQL', status: 'running' as const, detail: '连接池 32/64' },
  { name: 'MPHF 热缓存', status: 'running' as const, detail: 'HotMal + HotCle 已加载' },
  { name: 'JWT 鉴权', status: 'running' as const, detail: 'RS256 公钥已配置' },
]

const iconPaths: Record<string, string> = {
  search: '<circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>',
  upload: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" x2="12" y1="3" y2="15"/>',
  database: '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5V19A9 3 0 0 0 21 19V5"/><path d="M3 12A9 3 0 0 0 21 12"/>',
  power: '<path d="M12 2v10"/><path d="M18.4 6.6a9 9 0 1 1-12.77.04"/>',
  alert: '<path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"/><path d="M12 9v4"/><path d="M12 17h.01"/>',
  activity: '<path d="M22 12h-2.48a2 2 0 0 0-1.93 1.46l-2.35 8.36a.25.25 0 0 1-.48 0L9.24 2.18a.25.25 0 0 0-.48 0l-2.35 8.36A2 2 0 0 1 4.49 12H2"/>',
}

const colorMap: Record<string, string> = {
  blue: '#007aff',
  green: '#34c759',
  orange: '#ff9f0a',
  red: '#ff3b30',
}
</script>

<template>
  <div class="dashboard">
    <section class="page-intro">
      <div class="intro-text">
        <h1 class="page-title">系统总览</h1>
        <p class="page-desc">XaocSirck Cloud 缓存扫描管理平台，提供缓存查询、热/冷缓存管理、服务控制与系统监控功能。</p>
      </div>
      <div class="intro-actions">
        <RouterLink to="/cache/flow" class="btn-ghost">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <rect width="8" height="8" x="3" y="3" rx="2"/><path d="M7 11v4a2 2 0 0 0 2 2h4"/><rect width="8" height="8" x="13" y="13" rx="2"/>
          </svg>
          更新流程向导
        </RouterLink>
        <RouterLink to="/docs/api" class="btn-primary">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H20v20H6.5a2.5 2.5 0 0 1 0-5H20"/>
          </svg>
          查看 API 文档
        </RouterLink>
      </div>
    </section>

    <section class="stats-grid">
      <StatCard label="总查询量" :value="stats.total_requests.toLocaleString()" icon="activity" accent="blue" />
      <StatCard label="最近 1h 请求" :value="stats.requests_1h.toLocaleString()" icon="activity" accent="green" />
      <StatCard label="最近 1天 请求" :value="stats.requests_1d.toLocaleString()" icon="activity" accent="orange" />
      <StatCard label="平均响应" :value="stats.avg_duration_ms.toFixed(2)" unit="ms" icon="activity" accent="neutral" />
    </section>

    <div class="row-2">
      <BaseCard title="快捷操作" desc="常用功能入口">
        <div class="quick-grid">
          <button
            v-for="link in quickLinks"
            :key="link.title"
            class="quick-card"
            @click="router.push(link.to)"
          >
            <div class="quick-icon" :style="{ background: colorMap[link.color] + '15', color: colorMap[link.color] }">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" v-html="iconPaths[link.icon]"></svg>
            </div>
            <div class="quick-text">
              <span class="quick-title">{{ link.title }}</span>
              <span class="quick-desc">{{ link.desc }}</span>
            </div>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="quick-arrow">
              <path d="m9 18 6-6-6-6" />
            </svg>
          </button>
        </div>
      </BaseCard>

      <BaseCard title="服务状态" desc="核心组件运行状态">
        <div class="status-list">
          <div v-for="s in systemStatus" :key="s.name" class="status-item">
            <div class="status-info">
              <span class="status-name">{{ s.name }}</span>
              <span class="status-detail">{{ s.detail }}</span>
            </div>
            <StatusBadge :status="s.status" />
          </div>
        </div>
        <div class="service-endpoint">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <rect width="20" height="8" x="2" y="2" rx="2"/><rect width="20" height="8" x="2" y="14" rx="2"/><line x1="6" x2="6.01" y1="6" y2="6"/><line x1="6" x2="6.01" y1="18" y2="18"/>
          </svg>
          <span>API 端点：</span>
          <code>http://101.132.25.27:5100</code>
        </div>
      </BaseCard>
    </div>
  </div>
</template>

<style scoped>
.dashboard {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.page-intro {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1.5rem;
  flex-wrap: wrap;
}

.intro-text { display: flex; flex-direction: column; gap: 0.375rem; }

.page-title { font-size: 1.5rem; font-weight: 700; color: #1d1d1f; letter-spacing: -0.02em; }
.page-desc { font-size: 0.8125rem; color: rgba(60, 60, 67, 0.55); max-width: 38rem; line-height: 1.5; }

.intro-actions { display: flex; align-items: center; gap: 0.5rem; }

.btn-ghost {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.5rem 1rem;
  border-radius: 9999px;
  font-size: 0.8125rem;
  font-weight: 500;
  color: rgba(60, 60, 67, 0.8);
  background: rgba(255, 255, 255, 0.5);
  border: 1px solid rgba(0, 0, 0, 0.08);
  cursor: pointer;
  transition: all 150ms ease;
  font-family: inherit;
  text-decoration: none;
  backdrop-filter: blur(10px);
}

.btn-ghost:hover { background: rgba(255, 255, 255, 0.8); color: #1d1d1f; }

.btn-primary {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.5rem 1.125rem;
  border-radius: 9999px;
  font-size: 0.8125rem;
  font-weight: 600;
  color: #ffffff;
  background: linear-gradient(135deg, #007aff 0%, #0064d6 100%);
  border: none;
  cursor: pointer;
  transition: all 200ms cubic-bezier(0.32, 0.72, 0, 1);
  font-family: inherit;
  text-decoration: none;
  box-shadow: 0 1px 3px rgba(0, 122, 255, 0.25);
}

.btn-primary:hover { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.35); }

.stats-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1rem; }

.row-2 { display: grid; grid-template-columns: 1.5fr 1fr; gap: 1.5rem; }

.quick-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }

.quick-card {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  border-radius: 0.875rem;
  border: 1px solid rgba(0, 0, 0, 0.05);
  background: rgba(255, 255, 255, 0.3);
  cursor: pointer;
  transition: all 200ms ease;
  font-family: inherit;
  text-align: left;
}

.quick-card:hover {
  background: rgba(255, 255, 255, 0.6);
  border-color: rgba(0, 122, 255, 0.15);
  transform: translateY(-1px);
}

.quick-icon {
  width: 2.5rem; height: 2.5rem;
  border-radius: 0.625rem;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.quick-text { display: flex; flex-direction: column; flex: 1; gap: 0.0625rem; }
.quick-title { font-size: 0.8125rem; font-weight: 600; color: #1d1d1f; }
.quick-desc { font-size: 0.6875rem; color: rgba(60, 60, 67, 0.5); }

.quick-arrow { color: rgba(60, 60, 67, 0.3); flex-shrink: 0; }

.status-list { display: flex; flex-direction: column; gap: 0.5rem; }

.status-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.625rem 0.75rem;
  border-radius: 0.625rem;
  background: rgba(0, 0, 0, 0.02);
  border: 1px solid rgba(0, 0, 0, 0.04);
}

.status-info { display: flex; flex-direction: column; gap: 0.0625rem; }
.status-name { font-size: 0.8125rem; font-weight: 500; color: #1d1d1f; }
.status-detail { font-size: 0.6875rem; font-family: 'JetBrains Mono', monospace; color: rgba(60, 60, 67, 0.5); }

.service-endpoint {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  margin-top: 0.75rem;
  padding: 0.5rem 0.75rem;
  border-radius: 0.625rem;
  background: rgba(0, 0, 0, 0.03);
  font-size: 0.6875rem;
  color: rgba(60, 60, 67, 0.5);
}

.service-endpoint svg { color: rgba(60, 60, 67, 0.4); }
.service-endpoint code { font-family: 'JetBrains Mono', monospace; font-size: 0.6875rem; color: #007aff; }

@media (max-width: 1280px) {
  .stats-grid { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 1024px) {
  .row-2 { grid-template-columns: 1fr; }
}

@media (max-width: 768px) {
  .stats-grid { grid-template-columns: 1fr; }
  .quick-grid { grid-template-columns: 1fr; }
}
</style>
