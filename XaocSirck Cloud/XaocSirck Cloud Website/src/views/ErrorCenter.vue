<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import BaseCard from '@/components/BaseCard.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { useToast } from '@/composables/useToast'
import { getErrors, type ErrorLog as ApiErrorLog } from '@/api/system'

const toast = useToast()
const logs = ref<ApiErrorLog[]>([])
const loading = ref(false)

const subsystems = ['全部', 'cache-service', 'system-host']
const activeSub = ref('全部')
const levels = ['全部', 'error', 'warning', 'info']
const activeLevel = ref('全部')

async function loadErrors() {
  loading.value = true
  try {
    logs.value = await getErrors({
      subsystem: activeSub.value === '全部' ? undefined : activeSub.value,
      level: activeLevel.value === '全部' ? undefined : activeLevel.value,
      limit: 100,
    })
  } catch (e) {
    toast.error('加载错误日志失败：' + (e instanceof Error ? e.message : '未知错误'))
  } finally {
    loading.value = false
  }
}

function filterLogs() {
  loadErrors()
}

const callbackSources = [
  { name: 'on_error_handler_01', subsystem: 'cache-service', registered: true, calls: 142 },
  { name: 'on_timeout_handler', subsystem: 'cache-service', registered: true, calls: 38 },
  { name: 'auth_error_cb', subsystem: 'system-host', registered: true, calls: 7 },
  { name: 'rate_limit_cb', subsystem: 'system-host', registered: true, calls: 23 },
  { name: 'disk_alert_cb', subsystem: 'system-host', registered: true, calls: 4 },
  { name: 'on_reload_cb', subsystem: 'cache-service', registered: true, calls: 12 },
  { name: 'network_failover_cb', subsystem: 'system-host', registered: false, calls: 0 },
]

const forwardRecords = [
  { time: '2026-07-10 15:32:08', target: 'monitoring@xaocsirck.cloud', status: 'delivered', retry: 0 },
  { time: '2026-07-10 15:28:41', target: 'ops-webhook /alerts', status: 'delivered', retry: 0 },
  { time: '2026-07-10 14:58:22', target: 'monitoring@xaocsirck.cloud', status: 'delivered', retry: 1 },
  { time: '2026-07-10 14:45:17', target: 'slack #ops-alerts', status: 'delivered', retry: 0 },
  { time: '2026-07-10 14:12:09', target: 'ops-webhook /alerts', status: 'failed', retry: 3 },
  { time: '2026-07-10 13:58:33', target: 'monitoring@xaocsirck.cloud', status: 'delivered', retry: 0 },
]

const levelMap = {
  error: { status: 'error' as const, label: 'ERROR' },
  warning: { status: 'warning' as const, label: 'WARN' },
  info: { status: 'success' as const, label: 'INFO' },
}

function formatTime(iso: string): string {
  return iso.replace('T', ' ').replace('Z', ' UTC')
}

onMounted(loadErrors)
watch([activeSub, activeLevel], loadErrors)
</script>

<template>
  <div class="error-center">
    <BaseCard title="告警日志" desc="各子系统报错记录，支持按子系统和级别筛选">
      <template #toolbar>
        <div class="filter-row">
          <div class="filter-group">
            <span class="filter-label">子系统</span>
            <button
              v-for="s in subsystems" :key="s"
              class="chip" :class="{ active: activeSub === s }"
              @click="activeSub = s; filterLogs()"
            >{{ s }}</button>
          </div>
          <div class="filter-group">
            <span class="filter-label">级别</span>
            <button
              v-for="l in levels" :key="l"
              class="chip" :class="{ active: activeLevel === l }"
              @click="activeLevel = l; filterLogs()"
            >{{ l }}</button>
          </div>
        </div>
      </template>
      <div v-if="loading" class="loading-text">加载中…</div>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>时间</th>
              <th>子系统</th>
              <th>级别</th>
              <th>消息</th>
              <th>回调来源</th>
              <th>转发</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="log in logs" :key="log.id">
              <td><span class="cell-time">{{ formatTime(log.time) }}</span></td>
              <td><span class="cell-sub">{{ log.subsystem }}</span></td>
              <td><StatusBadge :status="levelMap[log.level].status" :label="levelMap[log.level].label" /></td>
              <td><span class="cell-msg">{{ log.message }}</span></td>
              <td><span class="cell-cb">{{ log.callback || '—' }}</span></td>
              <td>
                <span v-if="log.forwarded" class="fwd-yes">已转发</span>
                <span v-else class="fwd-no">未转发</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </BaseCard>

    <div class="row-2">
      <BaseCard title="已注册错误回调" desc="子系统注册的错误回调处理器">
        <div class="cb-list">
          <div v-for="cb in callbackSources" :key="cb.name" class="cb-item">
            <div class="cb-info">
              <span class="cb-name">{{ cb.name }}</span>
              <span class="cb-sub">{{ cb.subsystem }}</span>
            </div>
            <div class="cb-stats">
              <span class="cb-calls">{{ cb.calls }} 次调用</span>
              <span class="cb-reg" :class="{ on: cb.registered }">{{ cb.registered ? '已注册' : '未注册' }}</span>
            </div>
          </div>
        </div>
      </BaseCard>

      <BaseCard title="转发记录" desc="告警转发到外部系统的记录">
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>时间</th>
                <th>目标</th>
                <th>状态</th>
                <th>重试</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="r in forwardRecords" :key="r.time + r.target">
                <td><span class="cell-time">{{ r.time }}</span></td>
                <td><span class="cell-target">{{ r.target }}</span></td>
                <td>
                  <span class="fwd-status" :class="r.status">{{ r.status === 'delivered' ? '已送达' : '失败' }}</span>
                </td>
                <td><span class="cell-retry">{{ r.retry }}</span></td>
              </tr>
            </tbody>
          </table>
        </div>
      </BaseCard>
    </div>
  </div>
</template>

<style scoped>
.error-center {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.filter-row {
  display: flex;
  gap: 1.5rem;
  flex-wrap: wrap;
  margin-top: 0.5rem;
}

.filter-group {
  display: flex;
  align-items: center;
  gap: 0.375rem;
}

.filter-label {
  font-size: 0.6875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(60, 60, 67, 0.5);
  margin-right: 0.25rem;
}

.chip {
  padding: 0.1875rem 0.5625rem;
  border-radius: 9999px;
  font-size: 0.6875rem;
  font-weight: 500;
  color: rgba(60, 60, 67, 0.7);
  background: rgba(0, 0, 0, 0.03);
  border: 1px solid transparent;
  cursor: pointer;
  transition: all 150ms ease;
  font-family: inherit;
}

.chip:hover { background: rgba(0, 0, 0, 0.06); }

.chip.active {
  background: rgba(0, 122, 255, 0.1);
  color: #007aff;
  border-color: rgba(0, 122, 255, 0.2);
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

.cell-time { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: rgba(60, 60, 67, 0.5); }
.cell-sub { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: #007aff; }
.cell-msg { font-size: 0.8125rem; color: #1d1d1f; max-width: 24rem; overflow: hidden; text-overflow: ellipsis; }
.cell-cb { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: rgba(60, 60, 67, 0.6); }
.cell-target { font-size: 0.75rem; color: rgba(60, 60, 67, 0.7); }
.cell-retry { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: rgba(60, 60, 67, 0.5); }

.fwd-yes { font-size: 0.6875rem; color: #1a7d34; font-weight: 500; }
.fwd-no { font-size: 0.6875rem; color: rgba(60, 60, 67, 0.4); }

.fwd-status { font-size: 0.6875rem; font-weight: 500; }
.fwd-status.delivered { color: #1a7d34; }
.fwd-status.failed { color: #c22b22; }

.row-2 {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
}

.cb-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.cb-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.625rem 0.75rem;
  border-radius: 0.625rem;
  background: rgba(0, 0, 0, 0.02);
  border: 1px solid rgba(0, 0, 0, 0.04);
  transition: background 150ms ease;
}

.cb-item:hover { background: rgba(0, 0, 0, 0.035); }

.cb-info { display: flex; flex-direction: column; gap: 0.125rem; }
.cb-name { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; font-weight: 500; color: #1d1d1f; }
.cb-sub { font-size: 0.6875rem; color: rgba(60, 60, 67, 0.5); }

.cb-stats { display: flex; align-items: center; gap: 0.75rem; }
.cb-calls { font-family: 'JetBrains Mono', monospace; font-size: 0.6875rem; color: rgba(60, 60, 67, 0.5); }
.cb-reg { font-size: 0.6875rem; font-weight: 500; padding: 0.0625rem 0.375rem; border-radius: 0.25rem; }
.cb-reg.on { background: rgba(52, 199, 89, 0.12); color: #1a7d34; }
.cb-reg:not(.on) { background: rgba(142, 142, 147, 0.12); color: #6e6e73; }

.loading-text { font-size: 0.8125rem; color: #004fad; margin-bottom: 0.5rem; }

@media (max-width: 1024px) {
  .row-2 { grid-template-columns: 1fr; }
}
</style>
