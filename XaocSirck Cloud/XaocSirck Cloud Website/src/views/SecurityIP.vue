<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import BaseCard from '@/components/BaseCard.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { useToast } from '@/composables/useToast'
import { getIPStats, getBlacklist, addBlacklist, removeBlacklist, getRouteStats, type IPRecord, type BlacklistEntry, type RouteStat } from '@/api/system'

const toast = useToast()
const ipRecords = ref<IPRecord[]>([])
const blacklist = ref<BlacklistEntry[]>([])
const routeStats = ref<RouteStat[]>([])
const loading = ref(false)

const newBlackIP = ref('')
const newReason = ref('手动添加')
const adding = ref(false)
const removing = ref<string | null>(null)

async function loadAll() {
  loading.value = true
  try {
    const [ips, bl, routes] = await Promise.all([
      getIPStats({ limit: 50 }),
      getBlacklist(),
      getRouteStats(),
    ])
    ipRecords.value = ips
    blacklist.value = bl
    routeStats.value = routes
  } catch (e) {
    toast.error('加载安全数据失败：' + (e instanceof Error ? e.message : '未知错误'))
  } finally {
    loading.value = false
  }
}

async function addToBlacklist() {
  const ip = newBlackIP.value.trim()
  if (!ip) return
  adding.value = true
  try {
    await addBlacklist(ip, newReason.value)
    toast.success(`已将 ${ip} 加入黑名单`)
    newBlackIP.value = ''
    await loadAll()
  } catch (e) {
    toast.error('添加黑名单失败：' + (e instanceof Error ? e.message : '未知错误'))
  } finally {
    adding.value = false
  }
}

async function doRemoveBlacklist(ip: string) {
  removing.value = ip
  try {
    await removeBlacklist(ip)
    toast.success(`已将 ${ip} 移出黑名单`)
    await loadAll()
  } catch (e) {
    toast.error('移除黑名单失败：' + (e instanceof Error ? e.message : '未知错误'))
  } finally {
    removing.value = null
  }
}

function formatTime(iso?: string): string {
  if (!iso) return '—'
  return iso.replace('T', ' ').replace('Z', ' UTC')
}

const subsystems = computed(() => {
  const set = new Set<string>()
  for (const r of routeStats.value) {
    if (r.route.startsWith('/api/cache')) set.add('cache-service')
    else if (r.route.startsWith('/api/system')) set.add('system-host')
  }
  return Array.from(set).length > 0 ? Array.from(set) : ['cache-service', 'system-host']
})

const activeSub = ref('cache-service')

const filteredRoutes = computed(() => {
  const prefix = activeSub.value === 'cache-service' ? '/api/cache' : '/api/system'
  return routeStats.value.filter(r => r.route.startsWith(prefix))
})

const statusMap = {
  normal: { status: 'success' as const, label: '正常' },
  blocked: { status: 'error' as const, label: '已封禁' },
  'rate-limited': { status: 'warning' as const, label: '限流' },
}

const methodColor: Record<string, string> = {
  GET: 'get',
  POST: 'post',
}

onMounted(loadAll)
</script>

<template>
  <div class="security-ip">
    <div v-if="loading" class="loading-banner">加载中…</div>

    <BaseCard title="IP 请求统计" desc="每个 IP 的请求量与访问状态">
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>IP 地址</th>
              <th>请求次数</th>
              <th>最近访问</th>
              <th>状态</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in ipRecords" :key="r.ip">
              <td><span class="cell-ip">{{ r.ip }}</span></td>
              <td><span class="cell-req">{{ r.requests.toLocaleString() }}</span></td>
              <td><span class="cell-time">{{ formatTime(r.last_access) }}</span></td>
              <td><StatusBadge :status="statusMap[r.status].status" :label="statusMap[r.status].label" /></td>
              <td>
                <button v-if="r.status !== 'blocked'" class="btn-block" @click="newBlackIP = r.ip; addToBlacklist()">封禁</button>
                <span v-else class="cell-muted">—</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </BaseCard>

    <div class="row-2">
      <BaseCard title="黑名单管理" desc="封禁的 IP 列表，支持增删">
        <div class="blacklist-add">
          <input
            v-model="newBlackIP"
            type="text"
            placeholder="输入 IP 地址…"
            class="ip-input"
            @keyup.enter="addToBlacklist"
          />
          <input
            v-model="newReason"
            type="text"
            placeholder="原因…"
            class="reason-input"
            @keyup.enter="addToBlacklist"
          />
          <button class="btn-add" :disabled="adding" @click="addToBlacklist">
            <svg v-if="adding" class="spin" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 12a9 9 0 1 1-6.219-8.56" />
            </svg>
            <svg v-else width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
              <path d="M5 12h14" /><path d="M12 5v14" />
            </svg>
            {{ adding ? '添加中…' : '添加' }}
          </button>
        </div>
        <div class="blacklist-list">
          <div v-for="b in blacklist" :key="b.ip" class="bl-item">
            <div class="bl-info">
              <span class="bl-ip">{{ b.ip }}</span>
              <span class="bl-reason">{{ b.reason }} · {{ formatTime(b.created_at) }}</span>
            </div>
            <button class="btn-remove" :disabled="removing === b.ip" @click="doRemoveBlacklist(b.ip)">
              <svg v-if="removing === b.ip" class="spin" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M21 12a9 9 0 1 1-6.219-8.56" />
              </svg>
              <svg v-else width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M3 6h18" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" /><path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
              </svg>
            </button>
          </div>
        </div>
      </BaseCard>

      <BaseCard title="子系统路由访问" desc="按子系统查看路由调用情况">
        <template #toolbar>
          <div class="sub-switch">
            <button
              v-for="s in subsystems" :key="s"
              class="sub-btn" :class="{ active: activeSub === s }"
              @click="activeSub = s"
            >{{ s }}</button>
          </div>
        </template>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>路由</th>
                <th>方法</th>
                <th>调用次数</th>
                <th>平均耗时</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="r in filteredRoutes" :key="r.route + r.method">
                <td><span class="cell-route">{{ r.route }}</span></td>
                <td><span class="method-tag" :class="methodColor[r.method]">{{ r.method }}</span></td>
                <td><span class="cell-req">{{ r.calls.toLocaleString() }}</span></td>
                <td><span class="cell-ms">{{ r.avg_ms.toFixed(2) }} ms</span></td>
              </tr>
            </tbody>
          </table>
        </div>
      </BaseCard>
    </div>
  </div>
</template>

<style scoped>
.security-ip {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.table-wrap { overflow-x: auto; }

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.8125rem;
}

thead th {
  text-align: left;
  padding: 0.5rem 0.75rem;
  font-size: 0.6875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(60, 60, 67, 0.5);
  white-space: nowrap;
  border-bottom: 1px solid rgba(0, 0, 0, 0.04);
}

tbody td {
  padding: 0.625rem 0.75rem;
  color: #1d1d1f;
  border-bottom: 1px solid rgba(0, 0, 0, 0.03);
  white-space: nowrap;
  vertical-align: middle;
}

tbody tr:hover { background: rgba(0, 122, 255, 0.025); }
tbody tr:last-child td { border-bottom: none; }

.cell-ip { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; font-weight: 500; color: #1d1d1f; }
.cell-req { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: rgba(60, 60, 67, 0.7); }
.cell-time { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: rgba(60, 60, 67, 0.5); }
.cell-muted { color: rgba(60, 60, 67, 0.3); }
.cell-route { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: #007aff; }
.cell-ms { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: rgba(60, 60, 67, 0.6); }

.btn-block {
  padding: 0.1875rem 0.5625rem;
  border-radius: 0.375rem;
  font-size: 0.6875rem;
  font-weight: 500;
  color: #c22b22;
  background: rgba(255, 59, 48, 0.08);
  border: none;
  cursor: pointer;
  transition: all 150ms ease;
  font-family: inherit;
}

.btn-block:hover { background: rgba(255, 59, 48, 0.15); }

.method-tag {
  padding: 0.0625rem 0.375rem;
  border-radius: 0.25rem;
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.625rem;
  font-weight: 600;
}

.method-tag.get { background: rgba(52, 199, 89, 0.12); color: #1a7d34; }
.method-tag.post { background: rgba(0, 122, 255, 0.12); color: #004fad; }

.row-2 {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
}

.blacklist-add {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}

.ip-input, .reason-input {
  padding: 0.5rem 0.75rem;
  border-radius: 0.625rem;
  border: 1px solid rgba(0, 0, 0, 0.08);
  background: rgba(255, 255, 255, 0.5);
  font-size: 0.8125rem;
  font-family: 'JetBrains Mono', monospace;
  color: #1d1d1f;
  outline: none;
  transition: all 150ms ease;
}

.ip-input { flex: 1; }
.reason-input { width: 8rem; }

.ip-input:focus, .reason-input:focus {
  border-color: #007aff;
  box-shadow: 0 0 0 3px rgba(0, 122, 255, 0.12);
  background: rgba(255, 255, 255, 0.9);
}

.ip-input::placeholder, .reason-input::placeholder { color: rgba(60, 60, 67, 0.4); }

.btn-add {
  display: flex;
  align-items: center;
  gap: 0.3125rem;
  padding: 0.5rem 0.875rem;
  border-radius: 0.625rem;
  font-size: 0.8125rem;
  font-weight: 600;
  color: #ffffff;
  background: linear-gradient(135deg, #007aff 0%, #0064d6 100%);
  border: none;
  cursor: pointer;
  transition: all 200ms ease;
  font-family: inherit;
  box-shadow: 0 1px 3px rgba(0, 122, 255, 0.25);
}

.btn-add:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3); }
.btn-add:disabled { opacity: 0.5; cursor: not-allowed; }

.blacklist-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.bl-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.625rem 0.75rem;
  border-radius: 0.625rem;
  background: rgba(255, 59, 48, 0.03);
  border: 1px solid rgba(255, 59, 48, 0.08);
}

.bl-info { display: flex; flex-direction: column; gap: 0.125rem; }
.bl-ip { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; font-weight: 500; color: #1d1d1f; }
.bl-reason { font-size: 0.6875rem; color: rgba(60, 60, 67, 0.5); }

.btn-remove {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 1.75rem;
  height: 1.75rem;
  border-radius: 0.375rem;
  border: none;
  background: transparent;
  color: rgba(60, 60, 67, 0.4);
  cursor: pointer;
  transition: all 150ms ease;
}

.btn-remove:hover:not(:disabled) { background: rgba(255, 59, 48, 0.1); color: #ff3b30; }
.btn-remove:disabled { opacity: 0.5; cursor: not-allowed; }

.sub-switch {
  display: flex;
  gap: 0.25rem;
  margin-top: 0.5rem;
}

.sub-btn {
  padding: 0.25rem 0.625rem;
  border-radius: 9999px;
  font-size: 0.6875rem;
  font-family: 'JetBrains Mono', monospace;
  font-weight: 500;
  color: rgba(60, 60, 67, 0.7);
  background: rgba(0, 0, 0, 0.03);
  border: 1px solid transparent;
  cursor: pointer;
  transition: all 150ms ease;
}

.sub-btn:hover { background: rgba(0, 0, 0, 0.06); }

.sub-btn.active {
  background: rgba(0, 122, 255, 0.1);
  color: #007aff;
  border-color: rgba(0, 122, 255, 0.2);
}

.loading-banner {
  padding: 0.75rem 1rem;
  border-radius: 0.75rem;
  background: rgba(0, 122, 255, 0.08);
  color: #004fad;
  font-size: 0.8125rem;
}

.spin { animation: spin 0.8s linear infinite; }

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

@media (max-width: 1024px) {
  .row-2 { grid-template-columns: 1fr; }
}
</style>
