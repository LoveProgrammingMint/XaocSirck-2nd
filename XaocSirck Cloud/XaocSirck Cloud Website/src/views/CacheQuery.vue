<script setup lang="ts">
import { ref } from 'vue'
import BaseCard from '@/components/BaseCard.vue'
import { queryCache } from '@/api/cache'
import { useToast } from '@/composables/useToast'

const toast = useToast()

const sha256Input = ref('')
const loading = ref(false)
const queryResult = ref<null | { code: number; label: string; desc: string; color: string }>(null)
const queryHistory = ref<{ sha256: string; result: string; time: string }[]>([])

const resultMap: Record<number, { label: string; desc: string; color: string }> = {
  0: { label: '干净', desc: '该文件哈希在缓存中标记为安全', color: 'green' },
  1: { label: '恶意', desc: '该文件哈希在缓存中标记为恶意', color: 'red' },
  2: { label: '未查询到', desc: '热缓存与冷缓存中均未找到该哈希', color: 'neutral' },
  4: { label: '服务繁忙', desc: '服务暂停超时，请稍后重试', color: 'orange' },
}

async function doQuery() {
  const hash = sha256Input.value.trim().toLowerCase()
  if (hash.length !== 64 || !/^[0-9a-f]+$/.test(hash)) {
    toast.error('SHA256 格式无效：需要 64 位十六进制字符串')
    return
  }
  loading.value = true
  try {
    const code = await queryCache(hash)
    const info = resultMap[code] || { label: '未知', desc: `未知响应码: ${code}`, color: 'neutral' }
    queryResult.value = { code, ...info }
    queryHistory.value.unshift({
      sha256: hash.slice(0, 16) + '…' + hash.slice(-8),
      result: info.label,
      time: new Date().toLocaleTimeString('zh-CN', { hour12: false }),
    })
    if (queryHistory.value.length > 8) queryHistory.value.pop()
  } catch (e) {
    toast.error('查询失败：' + (e as Error).message)
  } finally {
    loading.value = false
  }
}

const sampleHash = 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'
</script>

<template>
  <div class="cache-query">
    <BaseCard title="SHA256 缓存查询" desc="调用 GET /api/cache/query，先查热缓存 MPHF，未命中再查 PostgreSQL 冷缓存">
      <div class="query-area">
        <div class="input-row">
          <div class="input-wrap">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="input-icon">
              <circle cx="11" cy="11" r="8" /><path d="m21 21-4.3-4.3" />
            </svg>
            <input
              v-model="sha256Input"
              type="text"
              placeholder="输入 64 位 SHA256 哈希值…"
              class="sha-input"
              @keyup.enter="doQuery"
            />
            <button class="btn-sample" @click="sha256Input = sampleHash">填充示例</button>
          </div>
          <button class="btn-query" :disabled="loading" @click="doQuery">
            <svg v-if="loading" class="spin" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 12a9 9 0 1 1-6.219-8.56" />
            </svg>
            {{ loading ? '查询中…' : '查询' }}
          </button>
        </div>

        <div v-if="queryResult" class="result-box" :class="queryResult.color">
          <div class="result-icon">
            <svg v-if="queryResult.code === 0" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="10" /><path d="m9 12 2 2 4-4" />
            </svg>
            <svg v-else-if="queryResult.code === 1" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="10" /><path d="m15 9-6 6" /><path d="m9 9 6 6" />
            </svg>
            <svg v-else-if="queryResult.code === 2" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="11" cy="11" r="8" /><path d="m21 21-4.3-4.3" /><path d="M8 11h6" />
            </svg>
            <svg v-else width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="10" /><path d="M12 6v6l4 2" />
            </svg>
          </div>
          <div class="result-info">
            <div class="result-label">{{ queryResult.label }}</div>
            <div class="result-desc">{{ queryResult.desc }}</div>
            <div class="result-code">返回字节: <span class="code-val">{{ queryResult.code }}</span></div>
          </div>
        </div>

        <div v-if="queryHistory.length > 0" class="history-section">
          <div class="history-title">最近查询</div>
          <div class="history-list">
            <div v-for="(h, i) in queryHistory" :key="i" class="history-item">
              <span class="h-hash">{{ h.sha256 }}</span>
              <span class="h-result" :class="h.result === '干净' ? 'green' : h.result === '恶意' ? 'red' : 'neutral'">{{ h.result }}</span>
              <span class="h-time">{{ h.time }}</span>
            </div>
          </div>
        </div>
      </div>
    </BaseCard>

    <div class="info-row">
      <BaseCard title="查询流程说明">
        <div class="flow-steps">
          <div class="flow-step">
            <div class="flow-num">1</div>
            <div class="flow-text">客户端发送 SHA256 到 <code>/api/cache/query</code></div>
          </div>
          <div class="flow-step">
            <div class="flow-num">2</div>
            <div class="flow-text">优先查询热缓存 MPHF（内存中，纳秒级响应）</div>
          </div>
          <div class="flow-step">
            <div class="flow-num">3</div>
            <div class="flow-text">未命中则查询 PostgreSQL 冷缓存</div>
          </div>
          <div class="flow-step">
            <div class="flow-num">4</div>
            <div class="flow-text">返回单字节结果：0=干净 / 1=恶意 / 2=未找到 / 4=繁忙</div>
          </div>
        </div>
      </BaseCard>
      <BaseCard title="接口信息">
        <div class="api-info">
          <div class="api-row"><span class="api-key">路径</span><code class="api-val">/api/cache/query</code></div>
          <div class="api-row"><span class="api-key">方法</span><code class="api-val">GET / POST</code></div>
          <div class="api-row"><span class="api-key">权限</span><span class="api-val pub">公开（无需 Token）</span></div>
          <div class="api-row"><span class="api-key">GET 参数</span><code class="api-val">?sha256=&lt;64位16进制&gt;</code></div>
          <div class="api-row"><span class="api-key">POST Body</span><code class="api-val">{ "sha256": "..." }</code></div>
          <div class="api-row"><span class="api-key">响应</span><span class="api-val">单字节 (0/1/2/4)</span></div>
        </div>
      </BaseCard>
    </div>
  </div>
</template>

<style scoped>
.cache-query {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.query-area {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.input-row {
  display: flex;
  gap: 0.75rem;
}

.input-wrap {
  flex: 1;
  position: relative;
  display: flex;
  align-items: center;
}

.input-icon {
  position: absolute;
  left: 0.75rem;
  color: rgba(60, 60, 67, 0.4);
  pointer-events: none;
}

.sha-input {
  width: 100%;
  padding: 0.625rem 5rem 0.625rem 2.25rem;
  border-radius: 0.75rem;
  border: 1px solid rgba(0, 0, 0, 0.08);
  background: rgba(255, 255, 255, 0.5);
  font-size: 0.8125rem;
  font-family: 'JetBrains Mono', monospace;
  color: #1d1d1f;
  outline: none;
  transition: all 150ms ease;
}

.sha-input:focus {
  border-color: #007aff;
  box-shadow: 0 0 0 3px rgba(0, 122, 255, 0.12);
  background: rgba(255, 255, 255, 0.9);
}

.sha-input::placeholder { color: rgba(60, 60, 67, 0.4); }

.btn-sample {
  position: absolute;
  right: 0.5rem;
  padding: 0.1875rem 0.5rem;
  border-radius: 0.375rem;
  font-size: 0.6875rem;
  font-weight: 500;
  color: rgba(60, 60, 67, 0.6);
  background: rgba(0, 0, 0, 0.04);
  border: none;
  cursor: pointer;
  transition: all 150ms ease;
  font-family: inherit;
}

.btn-sample:hover { background: rgba(0, 0, 0, 0.08); color: #007aff; }

.btn-query {
  padding: 0.625rem 1.5rem;
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
  white-space: nowrap;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.btn-query:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3); }
.btn-query:disabled { opacity: 0.5; cursor: not-allowed; }

.spin { animation: spin 0.8s linear infinite; }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }

.result-box {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem;
  border-radius: 1rem;
  border: 1px solid;
}

.result-box.green { background: rgba(52, 199, 89, 0.06); border-color: rgba(52, 199, 89, 0.2); color: #1a7d34; }
.result-box.red { background: rgba(255, 59, 48, 0.06); border-color: rgba(255, 59, 48, 0.2); color: #c22b22; }
.result-box.neutral { background: rgba(142, 142, 147, 0.06); border-color: rgba(142, 142, 147, 0.2); color: #6e6e73; }
.result-box.orange { background: rgba(255, 159, 10, 0.06); border-color: rgba(255, 159, 10, 0.2); color: #946000; }

.result-icon { flex-shrink: 0; }
.result-info { display: flex; flex-direction: column; gap: 0.25rem; }
.result-label { font-size: 1.25rem; font-weight: 700; }
.result-desc { font-size: 0.8125rem; color: rgba(60, 60, 67, 0.6); }
.result-code { font-size: 0.75rem; color: rgba(60, 60, 67, 0.5); font-family: 'JetBrains Mono', monospace; margin-top: 0.25rem; }
.code-val { font-weight: 600; }

.history-section { border-top: 1px solid rgba(0, 0, 0, 0.04); padding-top: 1rem; }
.history-title { font-size: 0.75rem; font-weight: 600; color: rgba(60, 60, 67, 0.5); margin-bottom: 0.5rem; }
.history-list { display: flex; flex-direction: column; gap: 0.375rem; }

.history-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem 0.75rem;
  border-radius: 0.5rem;
  background: rgba(0, 0, 0, 0.02);
}

.h-hash { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: rgba(60, 60, 67, 0.6); flex: 1; }
.h-result { font-size: 0.75rem; font-weight: 600; padding: 0.0625rem 0.5rem; border-radius: 9999px; }
.h-result.green { background: rgba(52, 199, 89, 0.12); color: #1a7d34; }
.h-result.red { background: rgba(255, 59, 48, 0.12); color: #c22b22; }
.h-result.neutral { background: rgba(142, 142, 147, 0.12); color: #6e6e73; }
.h-time { font-family: 'JetBrains Mono', monospace; font-size: 0.6875rem; color: rgba(60, 60, 67, 0.4); }

.info-row { display: grid; grid-template-columns: 1fr 1fr; gap: 1.5rem; }

.flow-steps { display: flex; flex-direction: column; gap: 0.75rem; }
.flow-step { display: flex; align-items: flex-start; gap: 0.625rem; }

.flow-num {
  width: 1.5rem; height: 1.5rem; border-radius: 50%;
  background: rgba(0, 122, 255, 0.1); color: #007aff;
  display: flex; align-items: center; justify-content: center;
  font-size: 0.75rem; font-weight: 600; font-family: 'JetBrains Mono', monospace;
  flex-shrink: 0;
}

.flow-text { font-size: 0.8125rem; color: rgba(60, 60, 67, 0.7); line-height: 1.5; padding-top: 0.0625rem; }
.flow-text code { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: #007aff; background: rgba(0, 122, 255, 0.06); padding: 0.0625rem 0.375rem; border-radius: 0.25rem; }

.api-info { display: flex; flex-direction: column; gap: 0.5rem; }
.api-row { display: flex; align-items: center; gap: 0.75rem; }
.api-key { font-size: 0.75rem; font-weight: 500; color: rgba(60, 60, 67, 0.5); min-width: 4.5rem; }
.api-val { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: #1d1d1f; background: rgba(0, 0, 0, 0.03); padding: 0.1875rem 0.5rem; border-radius: 0.375rem; }
.api-val.pub { color: #1a7d34; background: rgba(52, 199, 89, 0.08); }

@media (max-width: 1024px) {
  .info-row { grid-template-columns: 1fr; }
}
</style>
