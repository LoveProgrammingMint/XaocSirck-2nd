<script setup lang="ts">
import { ref } from 'vue'
import BaseCard from '@/components/BaseCard.vue'
import { useToast } from '@/composables/useToast'
import { stopService as apiStop, restartService as apiRestart } from '@/api/cache'

const toast = useToast()
const serviceStatus = ref<'running' | 'paused'>('running')
const actionLoading = ref(false)

async function stopService() {
  actionLoading.value = true
  try {
    const result = await apiStop()
    serviceStatus.value = 'paused'
    toast.success(`服务已暂停：${result}`)
  } catch (e) {
    toast.error('暂停失败：' + (e instanceof Error ? e.message : '未知错误'))
  } finally {
    actionLoading.value = false
  }
}

async function restartService() {
  actionLoading.value = true
  try {
    const result = await apiRestart()
    serviceStatus.value = 'running'
    toast.success(`服务已重启：${result}`)
  } catch (e) {
    toast.error('重启失败：' + (e instanceof Error ? e.message : '未知错误'))
  } finally {
    actionLoading.value = false
  }
}
</script>

<template>
  <div class="service-control">
    <BaseCard title="缓存服务状态" desc="当前服务运行状态与控制">
      <div class="status-area">
        <div class="status-display" :class="serviceStatus">
          <div class="status-glow"></div>
          <div class="status-ring">
            <svg v-if="serviceStatus === 'running'" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M22 12h-2.48a2 2 0 0 0-1.93 1.46l-2.35 8.36a.25.25 0 0 1-.48 0L9.24 2.18a.25.25 0 0 0-.48 0l-2.35 8.36A2 2 0 0 1 4.49 12H2" />
            </svg>
            <svg v-else width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect width="20" height="14" x="2" y="3" rx="2" />
              <line x1="10" x2="10" y1="7" y2="13" />
              <line x1="14" x2="14" y1="7" y2="13" />
            </svg>
          </div>
          <div class="status-text">
            <div class="status-label">{{ serviceStatus === 'running' ? '运行中' : '已暂停' }}</div>
            <div class="status-desc">
              {{ serviceStatus === 'running'
                ? '服务正常响应查询请求'
                : '新查询请求将阻塞等待，30s 超时返回字节 4'
              }}
            </div>
          </div>
        </div>

        <div class="control-buttons">
          <button
            v-if="serviceStatus === 'running'"
            class="btn-stop"
            :disabled="actionLoading"
            @click="stopService"
          >
            <svg v-if="actionLoading" class="spin" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 12a9 9 0 1 1-6.219-8.56" />
            </svg>
            <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect width="20" height="14" x="2" y="3" rx="2" />
              <line x1="10" x2="10" y1="7" y2="13" />
              <line x1="14" x2="14" y1="7" y2="13" />
            </svg>
            {{ actionLoading ? '处理中…' : '暂停服务' }}
          </button>
          <button
            class="btn-restart"
            :disabled="actionLoading"
            @click="restartService"
          >
            <svg v-if="actionLoading" class="spin" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 12a9 9 0 1 1-6.219-8.56" />
            </svg>
            <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
              <path d="M3 3v5h5" />
            </svg>
            {{ actionLoading ? '处理中…' : '重启服务' }}
          </button>
        </div>
      </div>
    </BaseCard>

    <div class="info-row">
      <BaseCard title="暂停服务说明" desc="POST /api/cache/stop">
        <div class="api-info">
          <div class="api-row"><span class="api-key">路径</span><code class="api-val">/api/cache/stop</code></div>
          <div class="api-row"><span class="api-key">方法</span><code class="api-val">POST</code></div>
          <div class="api-row"><span class="api-key">权限</span><span class="api-val auth">需 Token</span></div>
          <div class="api-row"><span class="api-key">请求体</span><code class="api-val">无</code></div>
          <div class="api-row"><span class="api-key">成功响应</span><code class="api-val">200 OK: stopped</code></div>
          <div class="api-row"><span class="api-key">影响</span><span class="api-val">查询请求阻塞 30s 后返回字节 4</span></div>
        </div>
      </BaseCard>

      <BaseCard title="重启服务说明" desc="POST /api/cache/restart">
        <div class="api-info">
          <div class="api-row"><span class="api-key">路径</span><code class="api-val">/api/cache/restart</code></div>
          <div class="api-row"><span class="api-key">方法</span><code class="api-val">POST</code></div>
          <div class="api-row"><span class="api-key">权限</span><span class="api-val auth">需 Token</span></div>
          <div class="api-row"><span class="api-key">请求体</span><code class="api-val">无</code></div>
          <div class="api-row"><span class="api-key">成功响应</span><code class="api-val">200 OK: restarted</code></div>
          <div class="api-row"><span class="api-key">影响</span><code class="api-val">恢复查询服务，解除阻塞</code></div>
        </div>
      </BaseCard>
    </div>
  </div>
</template>

<style scoped>
.service-control {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.status-area {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  align-items: center;
  padding: 1rem 0;
}

.status-display {
  display: flex;
  align-items: center;
  gap: 1.25rem;
  padding: 1.5rem 2rem;
  border-radius: 1.25rem;
  border: 1px solid;
  position: relative;
  overflow: hidden;
  width: 100%;
  max-width: 28rem;
}

.status-display.running {
  background: rgba(52, 199, 89, 0.04);
  border-color: rgba(52, 199, 89, 0.15);
}

.status-display.paused {
  background: rgba(255, 159, 10, 0.04);
  border-color: rgba(255, 159, 10, 0.15);
}

.status-glow {
  position: absolute;
  top: 50%;
  left: 4rem;
  transform: translateY(-50%);
  width: 6rem;
  height: 6rem;
  border-radius: 50%;
  filter: blur(40px);
  opacity: 0.15;
  pointer-events: none;
}

.status-display.running .status-glow { background: #34c759; }
.status-display.paused .status-glow { background: #ff9f0a; }

.status-ring {
  width: 4rem; height: 4rem;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  position: relative;
  z-index: 1;
}

.status-display.running .status-ring { background: rgba(52, 199, 89, 0.12); color: #34c759; }
.status-display.paused .status-ring { background: rgba(255, 159, 10, 0.12); color: #ff9f0a; }

.status-text { display: flex; flex-direction: column; gap: 0.25rem; z-index: 1; }

.status-label { font-size: 1.5rem; font-weight: 700; color: #1d1d1f; letter-spacing: -0.02em; }
.status-display.running .status-label { color: #1a7d34; }
.status-display.paused .status-label { color: #946000; }

.status-desc { font-size: 0.8125rem; color: rgba(60, 60, 67, 0.55); line-height: 1.4; }

.control-buttons {
  display: flex;
  gap: 0.75rem;
}

.btn-stop {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.625rem 1.5rem;
  border-radius: 0.75rem;
  font-size: 0.8125rem;
  font-weight: 600;
  color: #ffffff;
  background: linear-gradient(135deg, #ff9f0a 0%, #d6830a 100%);
  border: none;
  cursor: pointer;
  transition: all 200ms ease;
  font-family: inherit;
  box-shadow: 0 1px 3px rgba(255, 159, 10, 0.25);
}

.btn-stop:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(255, 159, 10, 0.3); }
.btn-stop:disabled { opacity: 0.5; cursor: not-allowed; }

.btn-restart {
  display: flex;
  align-items: center;
  gap: 0.5rem;
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
}

.btn-restart:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3); }
.btn-restart:disabled { opacity: 0.5; cursor: not-allowed; }

.spin { animation: spin 0.8s linear infinite; }

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.info-row { display: grid; grid-template-columns: 1fr 1fr; gap: 1.5rem; }

.api-info { display: flex; flex-direction: column; gap: 0.5rem; }
.api-row { display: flex; align-items: center; gap: 0.75rem; }
.api-key { font-size: 0.75rem; font-weight: 500; color: rgba(60, 60, 67, 0.5); min-width: 4.5rem; }
.api-val { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: #1d1d1f; background: rgba(0, 0, 0, 0.03); padding: 0.1875rem 0.5rem; border-radius: 0.375rem; }
.api-val.auth { color: #c2750a; background: rgba(255, 159, 10, 0.08); }

@media (max-width: 1024px) {
  .info-row { grid-template-columns: 1fr; }
}
</style>
