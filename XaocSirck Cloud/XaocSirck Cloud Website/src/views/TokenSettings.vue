<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import BaseCard from '@/components/BaseCard.vue'
import { useToast } from '@/composables/useToast'

const toast = useToast()

const tokenInput = ref('')
const showToken = ref(false)
const saving = ref(false)
const hasToken = ref(false)
const tokenPreview = ref('')

function loadTokenStatus() {
  const token = localStorage.getItem('xaoc_token')
  hasToken.value = !!token
  if (token) {
    tokenPreview.value = token.slice(0, 8) + '••••••••••••' + token.slice(-6)
  } else {
    tokenPreview.value = ''
  }
}

function tryDecodeJWT(token: string): { header?: Record<string, unknown>; payload?: Record<string, unknown>; error?: string } {
  const parts = token.split('.')
  if (parts.length !== 3) {
    return { error: '格式不正确：JWT 应包含 3 段（header.payload.signature）' }
  }
  try {
    const decode = (s: string) => {
      const normalized = s.replace(/-/g, '+').replace(/_/g, '/')
      const json = atob(normalized)
      return JSON.parse(json)
    }
    return { header: decode(parts[0]!), payload: decode(parts[1]!) }
  } catch {
    return { error: 'Base64 解码失败，请检查 token 是否完整' }
  }
}

const decodedInfo = computed(() => {
  if (!tokenInput.value.trim()) return null
  return tryDecodeJWT(tokenInput.value.trim())
})

async function saveToken() {
  const token = tokenInput.value.trim()
  if (!token) {
    toast.error('Token 不能为空')
    return
  }
  const decoded = tryDecodeJWT(token)
  if (decoded.error) {
    toast.error('Token 格式无效：' + decoded.error)
    return
  }
  saving.value = true
  await new Promise(r => setTimeout(r, 300))
  localStorage.setItem('xaoc_token', token)
  saving.value = false
  tokenInput.value = ''
  showToken.value = false
  loadTokenStatus()
  toast.success('Token 已保存，后续 API 请求将自动携带')
}

function clearToken() {
  localStorage.removeItem('xaoc_token')
  tokenInput.value = ''
  loadTokenStatus()
  toast.info('Token 已清除')
}

function copyPreview() {
  const token = localStorage.getItem('xaoc_token')
  if (token) {
    navigator.clipboard.writeText(token).then(() => {
      toast.success('完整 Token 已复制到剪贴板')
    }).catch(() => {
      toast.error('复制失败，请手动操作')
    })
  }
}

onMounted(loadTokenStatus)
</script>

<template>
  <div class="token-settings">
    <BaseCard title="当前 Token 状态" desc="检查浏览器中是否已存储有效的 JWT Token">
      <div class="status-area">
        <div class="status-indicator" :class="{ active: hasToken, empty: !hasToken }">
          <div class="status-dot"></div>
          <div class="status-info">
            <span class="status-title">{{ hasToken ? 'Token 已配置' : '未配置 Token' }}</span>
            <span v-if="hasToken" class="status-preview" @click="copyPreview">
              {{ tokenPreview }}
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="copy-icon">
                <rect width="14" height="14" x="8" y="8" rx="2" /><path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2" />
              </svg>
            </span>
            <span v-else class="status-hint">所有需鉴权的 API 请求将返回 401 Unauthorized</span>
          </div>
        </div>
        <button v-if="hasToken" class="btn-clear" @click="clearToken">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M3 6h18" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" /><path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
          </svg>
          清除 Token
        </button>
      </div>
    </BaseCard>

    <BaseCard title="写入 Token" desc="将后端签发的 RS256 JWT 粘贴到下方，保存后自动存入 localStorage">
      <div class="input-area">
        <div class="input-row">
          <div class="input-wrapper">
            <textarea
              v-model="tokenInput"
              :type="showToken ? 'text' : 'password'"
              class="token-input"
              :class="{ monospace: showToken }"
              placeholder="eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOi..."
              rows="4"
              spellcheck="false"
            />
            <button class="toggle-visibility" @click="showToken = !showToken">
              <svg v-if="showToken" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M9.88 9.88a3 3 0 1 0 4.24 4.24" /><path d="M10.73 5.08A10.43 10.43 0 0 1 12 5c7 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68" /><path d="M6.61 6.61A13.526 13.526 0 0 0 2 12s3 7 10 7a9.74 9.74 0 0 0 5.39-1.61" /><line x1="2" x2="22" y1="2" y2="22" />
              </svg>
              <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z" /><circle cx="12" cy="12" r="3" />
              </svg>
            </button>
          </div>
        </div>

        <div v-if="decodedInfo" class="decode-panel" :class="{ valid: !decodedInfo.error, invalid: !!decodedInfo.error }">
          <div v-if="decodedInfo.error" class="decode-error">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="10" /><path d="m15 9-6 6" /><path d="m9 9 6 6" />
            </svg>
            {{ decodedInfo.error }}
          </div>
          <div v-else class="decode-content">
            <div class="decode-section">
              <span class="decode-label">Algorithm</span>
              <code class="decode-value">{{ (decodedInfo.header as Record<string, unknown>)?.alg || '—' }}</code>
            </div>
            <div class="decode-section">
              <span class="decode-label">Type</span>
              <code class="decode-value">{{ (decodedInfo.header as Record<string, unknown>)?.typ || '—' }}</code>
            </div>
            <div v-if="(decodedInfo.payload as Record<string, unknown>)?.sub" class="decode-section">
              <span class="decode-label">Subject</span>
              <code class="decode-value">{{ (decodedInfo.payload as Record<string, unknown>).sub }}</code>
            </div>
            <div v-if="(decodedInfo.payload as Record<string, unknown>)?.iat" class="decode-section">
              <span class="decode-label">Issued At</span>
              <code class="decode-value">{{ new Date((decodedInfo.payload as Record<string, unknown>).iat as number * 1000).toLocaleString('zh-CN') }}</code>
            </div>
            <div v-if="(decodedInfo.payload as Record<string, unknown>)?.exp" class="decode-section">
              <span class="decode-label">Expires</span>
              <code class="decode-value">{{ new Date((decodedInfo.payload as Record<string, unknown>).exp as number * 1000).toLocaleString('zh-CN') }}</code>
            </div>
          </div>
        </div>

        <div class="action-row">
          <button class="btn-save" :disabled="saving || !tokenInput.trim()" @click="saveToken">
            <svg v-if="saving" class="spin" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 12a9 9 0 1 1-6.219-8.56" />
            </svg>
            <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" /><polyline points="17 21 17 13 7 13 7 21" /><polyline points="7 3 7 8 15 8" />
            </svg>
            {{ saving ? '保存中…' : '保存 Token' }}
          </button>
          <button class="btn-reset" @click="tokenInput = ''">清空输入</button>
        </div>
      </div>
    </BaseCard>

    <BaseCard title="使用说明" desc="Token 存储机制与鉴权流程">
      <div class="info-grid">
        <div class="info-item">
          <div class="info-icon blue">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4" />
            </svg>
          </div>
          <div class="info-text">
            <span class="info-title">存储位置</span>
            <span class="info-desc">Token 保存在浏览器 <code>localStorage.xaoc_token</code>，关闭浏览器后依然保留。</span>
          </div>
        </div>
        <div class="info-item">
          <div class="info-icon green">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
            </svg>
          </div>
          <div class="info-text">
            <span class="info-title">自动注入</span>
            <span class="info-desc">所有 API 请求通过 <code>client.ts</code> 自动在 Header 添加 <code>Authorization: Bearer &lt;token&gt;</code>。</span>
          </div>
        </div>
        <div class="info-item">
          <div class="info-icon orange">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" />
            </svg>
          </div>
          <div class="info-text">
            <span class="info-title">过期处理</span>
            <span class="info-desc">RS256 JWT 有 <code>exp</code> 过期时间，过期后需重新签发并粘贴新的 Token。</span>
          </div>
        </div>
        <div class="info-item">
          <div class="info-icon red">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect width="18" height="11" x="3" y="11" rx="2" ry="2" /><path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
          </div>
          <div class="info-text">
            <span class="info-title">安全提示</span>
            <span class="info-desc">Token 等同于管理员凭证，请勿在公共设备上保存。使用完毕后建议点击"清除 Token"。</span>
          </div>
        </div>
      </div>
    </BaseCard>
  </div>
</template>

<style scoped>
.token-settings {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.status-area {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}

.status-indicator {
  display: flex;
  align-items: center;
  gap: 0.875rem;
  padding: 1rem 1.25rem;
  border-radius: 0.875rem;
  border: 1px solid;
  flex: 1;
}

.status-indicator.active {
  background: rgba(52, 199, 89, 0.05);
  border-color: rgba(52, 199, 89, 0.15);
}

.status-indicator.empty {
  background: rgba(255, 59, 48, 0.04);
  border-color: rgba(255, 59, 48, 0.12);
}

.status-dot {
  width: 0.625rem;
  height: 0.625rem;
  border-radius: 50%;
  flex-shrink: 0;
  position: relative;
}

.status-indicator.active .status-dot {
  background: #34c759;
  box-shadow: 0 0 0 4px rgba(52, 199, 89, 0.15);
}

.status-indicator.empty .status-dot {
  background: #ff3b30;
  box-shadow: 0 0 0 4px rgba(255, 59, 48, 0.12);
}

.status-info {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
  min-width: 0;
}

.status-title {
  font-size: 0.9375rem;
  font-weight: 600;
  color: #1d1d1f;
}

.status-indicator.active .status-title { color: #1a7d34; }
.status-indicator.empty .status-title { color: #c22b22; }

.status-preview {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.75rem;
  color: rgba(60, 60, 67, 0.6);
  cursor: pointer;
  transition: color 150ms ease;
}

.status-preview:hover { color: #007aff; }

.copy-icon { opacity: 0.5; }
.status-preview:hover .copy-icon { opacity: 1; }

.status-hint {
  font-size: 0.75rem;
  color: rgba(60, 60, 67, 0.45);
}

.btn-clear {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.5rem 0.875rem;
  border-radius: 0.625rem;
  font-size: 0.8125rem;
  font-weight: 500;
  color: #c22b22;
  background: rgba(255, 59, 48, 0.06);
  border: 1px solid rgba(255, 59, 48, 0.12);
  cursor: pointer;
  transition: all 150ms ease;
  font-family: inherit;
  white-space: nowrap;
  flex-shrink: 0;
}

.btn-clear:hover { background: rgba(255, 59, 48, 0.12); border-color: rgba(255, 59, 48, 0.2); }

.input-area {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.input-wrapper {
  position: relative;
  width: 100%;
}

.token-input {
  width: 100%;
  padding: 0.75rem 2.75rem 0.75rem 0.875rem;
  border-radius: 0.75rem;
  border: 1px solid rgba(0, 0, 0, 0.1);
  background: rgba(255, 255, 255, 0.6);
  font-family: inherit;
  font-size: 0.8125rem;
  color: #1d1d1f;
  outline: none;
  resize: none;
  transition: all 150ms ease;
  line-height: 1.6;
  word-break: break-all;
}

.token-input.monospace {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.75rem;
}

.token-input::placeholder { color: rgba(60, 60, 67, 0.35); }

.token-input:focus {
  border-color: #007aff;
  box-shadow: 0 0 0 3px rgba(0, 122, 255, 0.12);
  background: rgba(255, 255, 255, 0.9);
}

.toggle-visibility {
  position: absolute;
  top: 0.75rem;
  right: 0.75rem;
  width: 1.75rem;
  height: 1.75rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 0.375rem;
  border: none;
  background: transparent;
  color: rgba(60, 60, 67, 0.4);
  cursor: pointer;
  transition: all 150ms ease;
}

.toggle-visibility:hover { background: rgba(0, 0, 0, 0.05); color: rgba(60, 60, 67, 0.7); }

.decode-panel {
  border-radius: 0.75rem;
  border: 1px solid;
  padding: 0.75rem;
}

.decode-panel.valid {
  background: rgba(52, 199, 89, 0.04);
  border-color: rgba(52, 199, 89, 0.12);
}

.decode-panel.invalid {
  background: rgba(255, 59, 48, 0.04);
  border-color: rgba(255, 59, 48, 0.12);
}

.decode-error {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.75rem;
  color: #c22b22;
}

.decode-content {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem 1.25rem;
}

.decode-section {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.decode-label {
  font-size: 0.625rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(60, 60, 67, 0.5);
}

.decode-value {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.75rem;
  color: #1d1d1f;
}

.action-row {
  display: flex;
  gap: 0.5rem;
  justify-content: flex-end;
}

.btn-save {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.5rem 1.125rem;
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

.btn-save:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3); }
.btn-save:disabled { opacity: 0.5; cursor: not-allowed; }

.btn-reset {
  padding: 0.5rem 1rem;
  border-radius: 0.625rem;
  font-size: 0.8125rem;
  font-weight: 500;
  color: rgba(60, 60, 67, 0.7);
  background: rgba(0, 0, 0, 0.04);
  border: none;
  cursor: pointer;
  font-family: inherit;
  transition: background 150ms ease;
}

.btn-reset:hover { background: rgba(0, 0, 0, 0.08); }

.spin { animation: spin 0.8s linear infinite; }

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.info-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}

.info-item {
  display: flex;
  align-items: flex-start;
  gap: 0.625rem;
  padding: 0.75rem;
  border-radius: 0.75rem;
  background: rgba(0, 0, 0, 0.02);
  border: 1px solid rgba(0, 0, 0, 0.04);
}

.info-icon {
  width: 1.75rem;
  height: 1.75rem;
  border-radius: 0.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.info-icon.blue { background: rgba(0, 122, 255, 0.1); color: #007aff; }
.info-icon.green { background: rgba(52, 199, 89, 0.1); color: #34c759; }
.info-icon.orange { background: rgba(255, 159, 10, 0.1); color: #ff9f0a; }
.info-icon.red { background: rgba(255, 59, 48, 0.1); color: #ff3b30; }

.info-text { display: flex; flex-direction: column; gap: 0.125rem; }

.info-title { font-size: 0.8125rem; font-weight: 600; color: #1d1d1f; }

.info-desc {
  font-size: 0.75rem;
  color: rgba(60, 60, 67, 0.55);
  line-height: 1.5;
}

.info-desc code {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.6875rem;
  color: #007aff;
  background: rgba(0, 122, 255, 0.06);
  padding: 0.0625rem 0.25rem;
  border-radius: 0.25rem;
}

@media (max-width: 768px) {
  .info-grid { grid-template-columns: 1fr; }
  .status-area { flex-direction: column; align-items: stretch; }
}
</style>
