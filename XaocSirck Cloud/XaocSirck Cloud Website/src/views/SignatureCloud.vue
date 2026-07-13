<script setup lang="ts">
import { ref, onMounted } from 'vue'
import BaseCard from '@/components/BaseCard.vue'
import { querySignature, addSignature, deleteSignature, getSignatures, clearSignatures, type SignatureEntry } from '@/api/signature'
import { useToast } from '@/composables/useToast'

const toast = useToast()

const queryInput = ref('')
const queryLoading = ref(false)
const queryResult = ref<null | { code: number; label: string; desc: string; color: string }>(null)

const resultMap: Record<number, { label: string; desc: string; color: string }> = {
  0: { label: '信任', desc: '该签名在信任列表中', color: 'green' },
  2: { label: '未查询到', desc: '签名云中未找到该签名', color: 'neutral' },
}

async function doQuery() {
  const signature = queryInput.value.trim()
  if (!signature) {
    toast.error('请输入签名')
    return
  }
  queryLoading.value = true
  queryResult.value = null
  try {
    const code = await querySignature(signature)
    const info = resultMap[code] || { label: '未知', desc: `未知响应码: ${code}`, color: 'neutral' }
    queryResult.value = { code, ...info }
  } catch (e) {
    toast.error('查询失败：' + (e as Error).message)
  } finally {
    queryLoading.value = false
  }
}

const entries = ref<SignatureEntry[]>([])
const loading = ref(false)
const currentPage = ref(1)
const pageSize = 20
const totalPages = ref(1)

const newSignature = ref('')
const adding = ref(false)

const deleteSignatureValue = ref('')
const deleting = ref(false)

const searchQuery = ref('')
const showClearConfirm = ref(false)
const clearing = ref(false)

async function loadEntries() {
  loading.value = true
  try {
    const offset = (currentPage.value - 1) * pageSize
    const result = await getSignatures({ limit: pageSize, offset })
    entries.value = Array.isArray(result) ? result : []
    if (entries.value.length === pageSize) {
      totalPages.value = currentPage.value + 1
    } else {
      totalPages.value = currentPage.value
    }
  } catch (e) {
    toast.error('加载签名云失败：' + (e as Error).message)
    entries.value = []
  } finally {
    loading.value = false
  }
}

async function doSearch() {
  if (!searchQuery.value.trim()) {
    await loadEntries()
    return
  }
  loading.value = true
  try {
    const result = await getSignatures({ signature: searchQuery.value.trim() })
    entries.value = Array.isArray(result) ? result : []
    totalPages.value = 1
    currentPage.value = 1
  } catch (e) {
    toast.error('查询失败：' + (e as Error).message)
  } finally {
    loading.value = false
  }
}

async function doAdd() {
  const signature = newSignature.value.trim()
  if (!signature) {
    toast.error('请输入签名')
    return
  }
  adding.value = true
  try {
    await addSignature(signature)
    toast.success('签名添加成功')
    newSignature.value = ''
    await loadEntries()
  } catch (e) {
    toast.error('添加失败：' + (e as Error).message)
  } finally {
    adding.value = false
  }
}

async function doDelete() {
  const signature = deleteSignatureValue.value.trim()
  if (!signature) {
    toast.error('请输入签名')
    return
  }
  deleting.value = true
  try {
    await deleteSignature(signature)
    toast.success('签名已删除')
    deleteSignatureValue.value = ''
    await loadEntries()
  } catch (e) {
    toast.error('删除失败：' + (e as Error).message)
  } finally {
    deleting.value = false
  }
}

async function doClear() {
  clearing.value = true
  try {
    const result = await clearSignatures()
    toast.success(`已清空 ${result.cleared} 条签名`)
    showClearConfirm.value = false
    currentPage.value = 1
    await loadEntries()
  } catch (e) {
    toast.error('清空失败：' + (e as Error).message)
  } finally {
    clearing.value = false
  }
}

function formatTime(iso: string): string {
  return iso.replace('T', ' ').replace('Z', ' UTC')
}

onMounted(loadEntries)
</script>

<template>
  <div class="signature-cloud">
    <BaseCard title="签名查询" desc="GET /api/signature/query — 公开查询，返回单字节 0/1/2">
      <div class="query-area">
        <div class="input-row">
          <div class="input-wrap">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="input-icon">
              <circle cx="11" cy="11" r="8" /><path d="m21 21-4.3-4.3" />
            </svg>
            <input v-model="queryInput" type="text" placeholder="输入签名字符串…" class="sig-input" @keyup.enter="doQuery" />
          </div>
          <button class="btn-query" :disabled="queryLoading" @click="doQuery">
            <svg v-if="queryLoading" class="spin" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 12a9 9 0 1 1-6.219-8.56" />
            </svg>
            {{ queryLoading ? '查询中…' : '查询' }}
          </button>
        </div>

        <div v-if="queryResult" class="result-box" :class="queryResult.color">
          <div class="result-info">
            <div class="result-label">{{ queryResult.label }}</div>
            <div class="result-desc">{{ queryResult.desc }}</div>
            <div class="result-code">返回字节: <span class="code-val">{{ queryResult.code }}</span></div>
          </div>
        </div>
      </div>
    </BaseCard>

    <div class="row-top">
      <BaseCard title="添加签名" desc="POST /api/signature/add — 录入信任的签名">
        <div class="add-form">
          <div class="form-row">
            <input v-model="newSignature" type="text" placeholder="输入签名字符串…" class="form-input" />
          </div>
          <button class="btn-add" :disabled="adding" @click="doAdd">{{ adding ? '添加中…' : '添加信任签名' }}</button>
        </div>
      </BaseCard>

      <BaseCard title="删除签名" desc="POST /api/signature/delete — 按签名删除">
        <div class="add-form">
          <div class="form-row">
            <input v-model="deleteSignatureValue" type="text" placeholder="输入要删除的签名…" class="form-input" />
          </div>
          <button class="btn-delete" :disabled="deleting" @click="doDelete">{{ deleting ? '删除中…' : '删除签名' }}</button>
        </div>
      </BaseCard>
    </div>

    <BaseCard title="签名云列表" desc="GET /api/signature/get — 分页查询">
      <template #toolbar>
        <div class="list-toolbar">
          <div class="search-wrap">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="search-icon"><circle cx="11" cy="11" r="8" /><path d="m21 21-4.3-4.3" /></svg>
            <input v-model="searchQuery" type="text" placeholder="搜索签名…" class="search-input" @keyup.enter="doSearch" />
          </div>
          <button class="btn-refresh" :disabled="loading" @click="loadEntries">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" /><path d="M3 3v5h5" /></svg>
            刷新
          </button>
          <button class="btn-clear-all" @click="showClearConfirm = true">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" /><path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" /></svg>
            清空签名云
          </button>
        </div>
      </template>
      <div class="table-wrap">
        <div v-if="loading" class="loading-state">加载中…</div>
        <div v-else-if="entries.length === 0" class="empty-state">暂无数据</div>
        <table v-else>
          <thead>
            <tr><th>签名</th><th>操作者 Token</th><th>创建时间</th></tr>
          </thead>
          <tbody>
            <tr v-for="entry in entries" :key="entry.signature">
              <td><span class="cell-signature" :title="entry.signature">{{ entry.signature }}</span></td>
              <td><span class="cell-token">{{ entry.operator_token }}</span></td>
              <td><span class="cell-time">{{ formatTime(entry.created_at) }}</span></td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="table-footer">
        <span class="footer-text">第 {{ currentPage }} / {{ totalPages }} 页</span>
        <div class="pagination">
          <button class="page-btn" :disabled="currentPage === 1" @click="currentPage--; loadEntries()">‹</button>
          <button class="page-btn active">{{ currentPage }}</button>
          <button class="page-btn" :disabled="currentPage >= totalPages" @click="currentPage++; loadEntries()">›</button>
        </div>
      </div>
    </BaseCard>

    <div v-if="showClearConfirm" class="modal-overlay" @click="showClearConfirm = false">
      <div class="modal" @click.stop>
        <div class="modal-icon">
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#ff3b30" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z" /><path d="M12 9v4" /><path d="M12 17h.01" /></svg>
        </div>
        <div class="modal-title">确认清空签名云？</div>
        <div class="modal-desc">此操作将删除所有签名云条目（POST /api/signature/clear），返回被清除的条目数。操作不可撤销。</div>
        <div class="modal-actions">
          <button class="btn-cancel" @click="showClearConfirm = false">取消</button>
          <button class="btn-confirm-danger" :disabled="clearing" @click="doClear">{{ clearing ? '清空中…' : '确认清空' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.signature-cloud { display: flex; flex-direction: column; gap: 1.5rem; }
.query-area { display: flex; flex-direction: column; gap: 1rem; }
.input-row { display: flex; gap: 0.75rem; }
.input-wrap { flex: 1; position: relative; display: flex; align-items: center; }
.input-icon { position: absolute; left: 0.75rem; color: rgba(60, 60, 67, 0.4); pointer-events: none; }
.sig-input { width: 100%; padding: 0.625rem 0.75rem 0.625rem 2.25rem; border-radius: 0.75rem; border: 1px solid rgba(0, 0, 0, 0.08); background: rgba(255, 255, 255, 0.5); font-size: 0.8125rem; font-family: 'JetBrains Mono', monospace; color: #1d1d1f; outline: none; transition: all 150ms ease; }
.sig-input:focus { border-color: #007aff; box-shadow: 0 0 0 3px rgba(0, 122, 255, 0.12); background: rgba(255, 255, 255, 0.9); }
.sig-input::placeholder { color: rgba(60, 60, 67, 0.4); }
.btn-query { padding: 0.625rem 1.5rem; border-radius: 0.75rem; font-size: 0.8125rem; font-weight: 600; color: #ffffff; background: linear-gradient(135deg, #007aff 0%, #0064d6 100%); border: none; cursor: pointer; transition: all 200ms ease; font-family: inherit; box-shadow: 0 1px 3px rgba(0, 122, 255, 0.25); white-space: nowrap; display: flex; align-items: center; gap: 0.5rem; }
.btn-query:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3); }
.btn-query:disabled { opacity: 0.5; cursor: not-allowed; }
.spin { animation: spin 0.8s linear infinite; }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
.result-box { display: flex; align-items: center; gap: 1rem; padding: 1rem 1.25rem; border-radius: 1rem; border: 1px solid; }
.result-box.green { background: rgba(52, 199, 89, 0.06); border-color: rgba(52, 199, 89, 0.2); color: #1a7d34; }
.result-box.red { background: rgba(255, 59, 48, 0.06); border-color: rgba(255, 59, 48, 0.2); color: #c22b22; }
.result-box.neutral { background: rgba(142, 142, 147, 0.06); border-color: rgba(142, 142, 147, 0.2); color: #6e6e73; }
.result-info { display: flex; flex-direction: column; gap: 0.25rem; }
.result-label { font-size: 1.125rem; font-weight: 700; }
.result-desc { font-size: 0.8125rem; color: rgba(60, 60, 67, 0.6); }
.result-code { font-size: 0.75rem; color: rgba(60, 60, 67, 0.5); font-family: 'JetBrains Mono', monospace; margin-top: 0.25rem; }
.code-val { font-weight: 600; }
.row-top { display: grid; grid-template-columns: 1fr 1fr; gap: 1.5rem; }
.add-form { display: flex; flex-direction: column; gap: 0.75rem; }
.form-row { display: flex; }
.form-input { width: 100%; padding: 0.625rem 0.75rem; border-radius: 0.625rem; border: 1px solid rgba(0, 0, 0, 0.08); background: rgba(255, 255, 255, 0.5); font-size: 0.8125rem; font-family: 'JetBrains Mono', monospace; color: #1d1d1f; outline: none; transition: all 150ms ease; }
.form-input:focus { border-color: #007aff; box-shadow: 0 0 0 3px rgba(0, 122, 255, 0.12); background: rgba(255, 255, 255, 0.9); }
.form-input::placeholder { color: rgba(60, 60, 67, 0.4); }
.btn-add { padding: 0.625rem 1.25rem; border-radius: 0.625rem; font-size: 0.8125rem; font-weight: 600; color: #ffffff; background: linear-gradient(135deg, #007aff 0%, #0064d6 100%); border: none; cursor: pointer; transition: all 200ms ease; font-family: inherit; box-shadow: 0 1px 3px rgba(0, 122, 255, 0.25); align-self: flex-start; }
.btn-add:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3); }
.btn-add:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-delete { padding: 0.625rem 1.25rem; border-radius: 0.625rem; font-size: 0.8125rem; font-weight: 600; color: #ffffff; background: linear-gradient(135deg, #ff3b30 0%, #d63028 100%); border: none; cursor: pointer; transition: all 200ms ease; font-family: inherit; box-shadow: 0 1px 3px rgba(255, 59, 48, 0.25); align-self: flex-start; }
.btn-delete:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(255, 59, 48, 0.3); }
.btn-delete:disabled { opacity: 0.5; cursor: not-allowed; }
.list-toolbar { display: flex; align-items: center; gap: 0.5rem; margin-top: 0.5rem; }
.search-wrap { position: relative; display: flex; align-items: center; flex: 1; }
.search-icon { position: absolute; left: 0.625rem; color: rgba(60, 60, 67, 0.4); pointer-events: none; }
.search-input { width: 100%; padding: 0.375rem 0.75rem 0.375rem 2rem; border-radius: 0.5rem; border: 1px solid rgba(0, 0, 0, 0.08); background: rgba(255, 255, 255, 0.5); font-size: 0.75rem; font-family: 'JetBrains Mono', monospace; color: #1d1d1f; outline: none; transition: all 150ms ease; }
.search-input:focus { border-color: #007aff; box-shadow: 0 0 0 3px rgba(0, 122, 255, 0.12); }
.search-input::placeholder { color: rgba(60, 60, 67, 0.4); }
.btn-refresh { display: flex; align-items: center; gap: 0.3125rem; padding: 0.375rem 0.75rem; border-radius: 0.5rem; font-size: 0.75rem; font-weight: 500; color: rgba(60, 60, 67, 0.7); background: rgba(0, 0, 0, 0.03); border: 1px solid transparent; cursor: pointer; font-family: inherit; white-space: nowrap; }
.btn-refresh:hover { background: rgba(0, 0, 0, 0.06); }
.btn-refresh:disabled { opacity: 0.5; }
.btn-clear-all { display: flex; align-items: center; gap: 0.3125rem; padding: 0.375rem 0.75rem; border-radius: 0.5rem; font-size: 0.75rem; font-weight: 500; color: #c22b22; background: rgba(255, 59, 48, 0.08); border: 1px solid rgba(255, 59, 48, 0.12); cursor: pointer; font-family: inherit; white-space: nowrap; }
.btn-clear-all:hover { background: rgba(255, 59, 48, 0.15); }
.table-wrap { overflow-x: auto; margin-top: 0.5rem; }
.loading-state, .empty-state { padding: 2rem; text-align: center; font-size: 0.8125rem; color: rgba(60, 60, 67, 0.4); }
table { width: 100%; border-collapse: collapse; font-size: 0.8125rem; }
thead th { text-align: left; padding: 0.5rem 0.75rem; font-size: 0.6875rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; color: rgba(60, 60, 67, 0.5); white-space: nowrap; border-bottom: 1px solid rgba(0, 0, 0, 0.04); }
tbody td { padding: 0.625rem 0.75rem; color: #1d1d1f; border-bottom: 1px solid rgba(0, 0, 0, 0.03); white-space: nowrap; vertical-align: middle; }
tbody tr:hover { background: rgba(0, 122, 255, 0.025); }
tbody tr:last-child td { border-bottom: none; }
.cell-signature { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: #007aff; max-width: 24rem; overflow: hidden; text-overflow: ellipsis; display: inline-block; }
.cell-token { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: rgba(60, 60, 67, 0.5); }
.cell-time { font-family: 'JetBrains Mono', monospace; font-size: 0.6875rem; color: rgba(60, 60, 67, 0.5); }
.table-footer { display: flex; align-items: center; justify-content: space-between; padding-top: 0.75rem; }
.footer-text { font-size: 0.75rem; color: rgba(60, 60, 67, 0.5); }
.pagination { display: flex; gap: 0.25rem; }
.page-btn { display: flex; align-items: center; justify-content: center; min-width: 1.75rem; height: 1.75rem; padding: 0 0.5rem; border-radius: 0.375rem; border: 1px solid rgba(0, 0, 0, 0.06); background: rgba(255, 255, 255, 0.5); color: rgba(60, 60, 67, 0.7); font-size: 0.75rem; font-weight: 500; font-family: 'JetBrains Mono', monospace; cursor: pointer; transition: all 150ms ease; }
.page-btn:hover:not(:disabled):not(.active) { background: rgba(0, 0, 0, 0.05); }
.page-btn.active { background: #007aff; border-color: #007aff; color: #fff; }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.modal-overlay { position: fixed; inset: 0; background: rgba(0, 0, 0, 0.3); backdrop-filter: blur(8px); -webkit-backdrop-filter: blur(8px); display: flex; align-items: center; justify-content: center; z-index: 100; }
.modal { background: rgba(255, 255, 255, 0.9); backdrop-filter: blur(40px) saturate(180%); -webkit-backdrop-filter: blur(40px) saturate(180%); border: 1px solid rgba(0, 0, 0, 0.06); border-radius: 1.5rem; padding: 2rem; max-width: 28rem; display: flex; flex-direction: column; align-items: center; text-align: center; gap: 1rem; }
.modal-icon { width: 3.5rem; height: 3.5rem; border-radius: 50%; background: rgba(255, 59, 48, 0.1); display: flex; align-items: center; justify-content: center; }
.modal-title { font-size: 1.125rem; font-weight: 700; color: #1d1d1f; }
.modal-desc { font-size: 0.8125rem; color: rgba(60, 60, 67, 0.6); line-height: 1.5; }
.modal-actions { display: flex; gap: 0.75rem; margin-top: 0.5rem; }
.btn-cancel { padding: 0.5rem 1.25rem; border-radius: 0.625rem; font-size: 0.8125rem; font-weight: 500; color: rgba(60, 60, 67, 0.7); background: rgba(0, 0, 0, 0.04); border: none; cursor: pointer; font-family: inherit; }
.btn-cancel:hover { background: rgba(0, 0, 0, 0.08); }
.btn-confirm-danger { padding: 0.5rem 1.25rem; border-radius: 0.625rem; font-size: 0.8125rem; font-weight: 600; color: #ffffff; background: linear-gradient(135deg, #ff3b30 0%, #d63028 100%); border: none; cursor: pointer; font-family: inherit; box-shadow: 0 1px 3px rgba(255, 59, 48, 0.25); }
.btn-confirm-danger:disabled { opacity: 0.5; cursor: not-allowed; }
@media (max-width: 1024px) { .row-top { grid-template-columns: 1fr; } }
</style>
