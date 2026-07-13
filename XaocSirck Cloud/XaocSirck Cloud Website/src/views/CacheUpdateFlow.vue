<script setup lang="ts">
import { ref } from 'vue'
import BaseCard from '@/components/BaseCard.vue'
import { buildHotCache, deleteHotCache } from '@/api/cache'

const building = ref(false)
const deleting = ref(false)
const selectedKind = ref<'all' | 'malicious' | 'clean'>('all')
const log = ref<string[]>([])
const error = ref('')

function addLog(line: string) {
  log.value.push(line)
}

async function doBuild() {
  building.value = true
  deleting.value = false
  error.value = ''
  log.value = []
  try {
    addLog('> POST /api/cache/build')
    const result = await buildHotCache()
    addLog(`< 200 OK: malicious=${result.malicious}, clean=${result.clean}`)
    addLog('> 热缓存已从冷缓存重建')
  } catch (e) {
    error.value = e instanceof Error ? e.message : '构建失败'
    addLog(`< ERROR: ${error.value}`)
  } finally {
    building.value = false
  }
}

async function doDeleteHot() {
  deleting.value = true
  building.value = false
  error.value = ''
  log.value = []
  try {
    const kind = selectedKind.value === 'all' ? undefined : selectedKind.value
    addLog(`> POST /api/cache/hot/delete kind=${selectedKind.value}`)
    await deleteHotCache(kind)
    addLog('< 200 OK: hot cache cleared')
    addLog('> 内存与文件中的热缓存已清除')
  } catch (e) {
    error.value = e instanceof Error ? e.message : '删除失败'
    addLog(`< ERROR: ${error.value}`)
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <div class="hot-cache-manage">
    <BaseCard title="热缓存管理" desc="自动每日 00:00 从冷缓存构建；也可手动强制构建或删除热缓存">
      <div class="info-row">
        <div class="info-item">
          <div class="info-title">自动储热</div>
          <div class="info-desc">服务启动后每天 00:00 UTC 自动从 cold_cache 表构建 HotMal.mphf 与 HotCle.mphf，并热加载到内存。</div>
        </div>
        <div class="info-item">
          <div class="info-title">三层查询</div>
          <div class="info-desc">查询顺序：热缓存 (MPHF) → 冷缓存 (PostgreSQL) → 未命中返回字节 2。</div>
        </div>
      </div>

      <div class="actions">
        <button class="btn-build" :disabled="building || deleting" @click="doBuild">
          <svg v-if="building" class="spin" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M21 12a9 9 0 1 1-6.219-8.56" />
          </svg>
          <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" /><path d="M3 3v5h5" />
          </svg>
          {{ building ? '构建中…' : '强制构建热缓存' }}
        </button>

        <div class="delete-group">
          <select v-model="selectedKind" class="kind-select" :disabled="building || deleting">
            <option value="all">全部</option>
            <option value="malicious">malicious</option>
            <option value="clean">clean</option>
          </select>
          <button class="btn-delete" :disabled="building || deleting" @click="doDeleteHot">
            <svg v-if="deleting" class="spin" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 12a9 9 0 1 1-6.219-8.56" />
            </svg>
            <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M3 6h18" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" /><path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
            </svg>
            {{ deleting ? '删除中…' : '删除热缓存' }}
          </button>
        </div>
      </div>

      <div v-if="error" class="error-text">{{ error }}</div>

      <div class="terminal" :class="{ active: log.length > 0 }">
        <div class="terminal-header">
          <div class="terminal-dots">
            <span class="dot red"></span>
            <span class="dot yellow"></span>
            <span class="dot green"></span>
          </div>
          <span class="terminal-title">xaocsirck-cache — hot-cache-builder</span>
        </div>
        <div class="terminal-body">
          <div v-if="log.length === 0" class="terminal-placeholder">等待操作…</div>
          <div
            v-for="(line, i) in log"
            :key="i"
            class="terminal-line"
            :class="{ out: line.startsWith('<'), cmd: line.startsWith('>'), err: line.startsWith('< ERROR') }"
          >
            {{ line }}
          </div>
          <div v-if="building || deleting" class="terminal-cursor">▋</div>
        </div>
      </div>
    </BaseCard>

    <BaseCard title="API 说明" desc="热缓存相关接口">
      <div class="api-list">
        <div class="api-row"><span class="api-key">POST</span><code class="api-val">/api/cache/build</code><span class="api-desc">从冷缓存强制构建热缓存</span></div>
        <div class="api-row"><span class="api-key">POST</span><code class="api-val">/api/cache/hot/delete</code><span class="api-desc">删除热缓存文件与内存；body 可选 { kind?: 'malicious' | 'clean' }</span></div>
        <div class="api-row"><span class="api-key">GET/POST</span><code class="api-val">/api/cache/query?sha256=...</code><span class="api-desc">公开查询，返回字节 0/1/2</span></div>
      </div>
    </BaseCard>
  </div>
</template>

<style scoped>
.hot-cache-manage {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.info-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
  margin-bottom: 1rem;
}

.info-item {
  padding: 0.75rem;
  border-radius: 0.75rem;
  background: rgba(0, 122, 255, 0.03);
  border: 1px solid rgba(0, 122, 255, 0.08);
}

.info-title { font-size: 0.8125rem; font-weight: 600; color: #1d1d1f; margin-bottom: 0.25rem; }
.info-desc { font-size: 0.75rem; color: rgba(60, 60, 67, 0.6); line-height: 1.4; }

.actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.btn-build {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.625rem 1.25rem;
  border-radius: 0.75rem;
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

.btn-build:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3); }

.delete-group { display: flex; align-items: center; gap: 0.5rem; }

.kind-select {
  padding: 0.5rem 0.75rem;
  border-radius: 0.625rem;
  border: 1px solid rgba(0, 0, 0, 0.08);
  background: rgba(255, 255, 255, 0.5);
  font-size: 0.8125rem;
  font-family: inherit;
  color: #1d1d1f;
  outline: none;
}

.btn-delete {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.625rem 1.25rem;
  border-radius: 0.75rem;
  font-size: 0.8125rem;
  font-weight: 600;
  color: #ffffff;
  background: linear-gradient(135deg, #ff3b30 0%, #d63028 100%);
  border: none;
  cursor: pointer;
  transition: all 200ms ease;
  font-family: inherit;
  box-shadow: 0 1px 3px rgba(255, 59, 48, 0.25);
}

.btn-delete:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(255, 59, 48, 0.3); }

button:disabled { opacity: 0.5; cursor: not-allowed; }

.error-text { font-size: 0.8125rem; color: #c22b22; }

.spin { animation: spin 0.8s linear infinite; }

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.terminal {
  background: #1d1d1f;
  border-radius: 0.75rem;
  overflow: hidden;
  opacity: 0.5;
}

.terminal.active { opacity: 1; }

.terminal-header {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  padding: 0.5rem 0.75rem;
  background: rgba(255, 255, 255, 0.05);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.terminal-dots { display: flex; gap: 0.375rem; }
.dot { width: 0.625rem; height: 0.625rem; border-radius: 50%; }
.dot.red { background: #ff5f56; }
.dot.yellow { background: #ffbd2e; }
.dot.green { background: #27c93f; }

.terminal-title { font-size: 0.6875rem; font-family: 'JetBrains Mono', monospace; color: rgba(255, 255, 255, 0.4); }

.terminal-body {
  padding: 0.75rem;
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.6875rem;
  line-height: 1.6;
  min-height: 8rem;
}

.terminal-placeholder { color: rgba(255, 255, 255, 0.25); }
.terminal-line { color: rgba(255, 255, 255, 0.7); white-space: pre-wrap; word-break: break-all; }
.terminal-line.cmd { color: #5ac8fa; }
.terminal-line.out { color: rgba(255, 255, 255, 0.5); }
.terminal-line.err { color: #ff6b6b; }

.terminal-cursor {
  display: inline-block;
  color: #34c759;
  animation: blink 1s step-end infinite;
}

@keyframes blink {
  50% { opacity: 0; }
}

.api-list { display: flex; flex-direction: column; gap: 0.5rem; }
.api-row { display: flex; align-items: center; gap: 0.75rem; font-size: 0.8125rem; }
.api-key {
  min-width: 4.5rem;
  padding: 0.125rem 0.375rem;
  border-radius: 0.25rem;
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.625rem;
  font-weight: 600;
  color: #004fad;
  background: rgba(0, 122, 255, 0.12);
}
.api-val { font-family: 'JetBrains Mono', monospace; color: #1d1d1f; background: rgba(0, 0, 0, 0.03); padding: 0.1875rem 0.5rem; border-radius: 0.375rem; }
.api-desc { color: rgba(60, 60, 67, 0.55); font-size: 0.75rem; }

@media (max-width: 1024px) {
  .info-row { grid-template-columns: 1fr; }
}
</style>
