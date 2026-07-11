<script setup lang="ts">
import BaseCard from '@/components/BaseCard.vue'

const baseUrl = 'http://101.132.25.27:5100'

interface Endpoint {
  path: string
  method: 'GET' | 'POST'
  auth: boolean
  desc: string
  request: string
  response: string
  note?: string
}

const endpoints: Endpoint[] = [
  {
    path: '/api/cache/query',
    method: 'GET',
    auth: false,
    desc: '查询缓存：先查热缓存 MPHF，未命中再查 PostgreSQL 冷缓存',
    request: 'GET ?sha256=<64位16进制字符串>\n或 POST { "sha256": "..." }',
    response: '单字节：0=干净 / 1=恶意 / 2=未查询到 / 4=服务繁忙',
    note: '公开接口，无需 Token',
  },
  {
    path: '/api/cache/update',
    method: 'POST',
    auth: true,
    desc: '更新热缓存：上传 .mphf 文件，覆盖并重新加载到内存',
    request: 'multipart/form-data\n  kind: malicious | clean\n  file: .mphf 二进制文件',
    response: '200: updated\n400: kind must be malicious or clean\n401: Unauthorized',
    note: '覆盖 ./MPHFs/HotMal.mphf 或 ./MPHFs/HotCle.mphf',
  },
  {
    path: '/api/cache/add',
    method: 'POST',
    auth: true,
    desc: '添加冷缓存条目：SHA256 + Label，已存在则更新',
    request: '{ "sha256": "e3b0...b855", "label": 1 }',
    response: '200: added\n400: invalid sha256\n401: Unauthorized',
    note: 'label: 0=干净, 1=恶意',
  },
  {
    path: '/api/cache/delete',
    method: 'POST',
    auth: true,
    desc: '删除冷缓存条目：按 SHA256 删除',
    request: '{ "sha256": "e3b0...b855" }',
    response: '200: deleted\n400: invalid sha256\n401: Unauthorized',
  },
  {
    path: '/api/cache/get',
    method: 'GET',
    auth: true,
    desc: '获取冷缓存：单条查询或列表查询',
    request: '单条: GET ?sha256=<hash>\n列表: GET ?limit=1000&offset=0',
    response: '单条: { sha256, label, operator_token, created_at }\n列表: [ { ... }, ... ]\n404: not found\n400: invalid sha256',
    note: '支持 POST 方法',
  },
  {
    path: '/api/cache/clear',
    method: 'POST',
    auth: true,
    desc: '清空冷缓存：删除全部条目',
    request: '无 Body',
    response: '{ "cleared": 123 }',
  },
  {
    path: '/api/cache/stop',
    method: 'POST',
    auth: true,
    desc: '暂停服务：新查询请求阻塞等待',
    request: '无 Body',
    response: '200: stopped',
    note: '暂停后 /api/cache/query 阻塞 30s 超时返回字节 4',
  },
  {
    path: '/api/cache/restart',
    method: 'POST',
    auth: true,
    desc: '重启服务：恢复查询服务',
    request: '无 Body',
    response: '200: restarted',
  },
]
</script>

<template>
  <div class="api-docs">
    <BaseCard title="缓存服务 API" desc="基础地址与鉴权说明">
      <div class="api-base">
        <div class="base-row">
          <span class="base-label">基础地址</span>
          <code class="base-value">{{ baseUrl }}</code>
        </div>
        <div class="base-row">
          <span class="base-label">鉴权方式</span>
          <div class="auth-detail">
            <code class="base-value">Authorization: Bearer &lt;JWT_TOKEN&gt;</code>
            <span class="auth-note">JWT 使用 RS256 签名，公钥配置在 settings.json 的 jwt_public_key 字段</span>
          </div>
        </div>
        <div class="base-row">
          <span class="base-label">公开接口</span>
          <code class="base-value pub">/api/cache/query</code>
          <span class="auth-note">— 其余接口均需 Token</span>
        </div>
      </div>
    </BaseCard>

    <div class="endpoint-list">
      <div v-for="(ep, i) in endpoints" :key="i" class="endpoint-card">
        <div class="ep-header">
          <span class="ep-method" :class="ep.method.toLowerCase()">{{ ep.method }}</span>
          <code class="ep-path">{{ ep.path }}</code>
          <span class="ep-auth" :class="{ required: ep.auth }">
            {{ ep.auth ? '需 Token' : '公开' }}
          </span>
        </div>
        <p class="ep-desc">{{ ep.desc }}</p>
        <div class="ep-sections">
          <div class="ep-section">
            <span class="ep-section-label">请求</span>
            <pre class="ep-code">{{ ep.request }}</pre>
          </div>
          <div class="ep-section">
            <span class="ep-section-label">响应</span>
            <pre class="ep-code">{{ ep.response }}</pre>
          </div>
        </div>
        <div v-if="ep.note" class="ep-note">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" />
          </svg>
          {{ ep.note }}
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.api-docs {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.api-base {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.base-row {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.base-label {
  font-size: 0.75rem;
  font-weight: 600;
  color: rgba(60, 60, 67, 0.5);
  min-width: 5rem;
  padding-top: 0.25rem;
}

.base-value {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.8125rem;
  color: #007aff;
  background: rgba(0, 122, 255, 0.06);
  padding: 0.25rem 0.625rem;
  border-radius: 0.375rem;
}

.base-value.pub { color: #1a7d34; background: rgba(52, 199, 89, 0.08); }

.auth-detail { display: flex; flex-direction: column; gap: 0.25rem; }
.auth-note { font-size: 0.75rem; color: rgba(60, 60, 67, 0.5); }

.endpoint-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.endpoint-card {
  background: rgba(255, 255, 255, 0.6);
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border: 1px solid rgba(0, 0, 0, 0.06);
  border-radius: 1.25rem;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.ep-header {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  flex-wrap: wrap;
}

.ep-method {
  padding: 0.1875rem 0.5rem;
  border-radius: 0.375rem;
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.6875rem;
  font-weight: 700;
  flex-shrink: 0;
}

.ep-method.get { background: rgba(52, 199, 89, 0.12); color: #1a7d34; }
.ep-method.post { background: rgba(0, 122, 255, 0.12); color: #004fad; }

.ep-path {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.875rem;
  font-weight: 600;
  color: #1d1d1f;
}

.ep-auth {
  font-size: 0.6875rem;
  font-weight: 500;
  padding: 0.125rem 0.5rem;
  border-radius: 9999px;
  margin-left: auto;
}

.ep-auth.required { background: rgba(255, 159, 10, 0.1); color: #946000; }
.ep-auth:not(.required) { background: rgba(52, 199, 89, 0.1); color: #1a7d34; }

.ep-desc {
  font-size: 0.8125rem;
  color: rgba(60, 60, 67, 0.6);
  line-height: 1.4;
}

.ep-sections {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}

.ep-section { display: flex; flex-direction: column; gap: 0.25rem; }

.ep-section-label {
  font-size: 0.6875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(60, 60, 67, 0.4);
}

.ep-code {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.75rem;
  line-height: 1.6;
  color: #1d1d1f;
  background: rgba(0, 0, 0, 0.03);
  padding: 0.625rem 0.75rem;
  border-radius: 0.625rem;
  white-space: pre-wrap;
  word-break: break-all;
  margin: 0;
}

.ep-note {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.75rem;
  color: rgba(60, 60, 67, 0.55);
  padding: 0.375rem 0.625rem;
  border-radius: 0.5rem;
  background: rgba(0, 122, 255, 0.04);
}

.ep-note svg { color: #007aff; flex-shrink: 0; }

@media (max-width: 768px) {
  .ep-sections { grid-template-columns: 1fr; }
}
</style>
