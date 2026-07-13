<script setup lang="ts">
import { ref } from 'vue'
import BaseCard from '@/components/BaseCard.vue'
import { buildHotCache, deleteHotCache } from '@/api/cache'
import { useToast } from '@/composables/useToast'

const toast = useToast()

const building = ref(false)
const deleting = ref(false)
const selectedKind = ref<'all' | 'malicious' | 'clean'>('all')

async function doBuild() {
  building.value = true
  try {
    const result = await buildHotCache()
    toast.success(`热缓存构建完成：malicious=${result.malicious}, clean=${result.clean}`)
  } catch (e) {
    toast.error('构建失败：' + (e as Error).message)
  } finally {
    building.value = false
  }
}

async function doDeleteHot() {
  deleting.value = true
  try {
    const kind = selectedKind.value === 'all' ? undefined : selectedKind.value
    await deleteHotCache(kind)
    toast.success('热缓存已删除')
  } catch (e) {
    toast.error('删除失败：' + (e as Error).message)
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <div class="hot-cache">
    <BaseCard title="热缓存管理" desc="自动每日 00:00 从冷缓存构建；支持手动强制构建与删除">
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
    </BaseCard>

    <BaseCard title="接口信息" desc="热缓存相关接口">
      <div class="api-info">
        <div class="api-row"><span class="api-key">路径</span><code class="api-val">POST /api/cache/build</code></div>
        <div class="api-row"><span class="api-key">说明</span><span class="api-val">从冷缓存强制构建热缓存</span></div>
        <div class="api-row"><span class="api-key">路径</span><code class="api-val">POST /api/cache/hot/delete</code></div>
        <div class="api-row"><span class="api-key">说明</span><span class="api-val">删除热缓存文件与内存；body 可选 { kind?: 'malicious' | 'clean' }</span></div>
      </div>
    </BaseCard>
  </div>
</template>

<style scoped>
.hot-cache { display: flex; flex-direction: column; gap: 1.5rem; }
.actions { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; }
.btn-build {
  display: flex; align-items: center; gap: 0.5rem; padding: 0.625rem 1.25rem; border-radius: 0.75rem;
  font-size: 0.8125rem; font-weight: 600; color: #ffffff;
  background: linear-gradient(135deg, #007aff 0%, #0064d6 100%); border: none; cursor: pointer;
  transition: all 200ms ease; font-family: inherit; box-shadow: 0 1px 3px rgba(0, 122, 255, 0.25);
}
.btn-build:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3); }
.delete-group { display: flex; align-items: center; gap: 0.5rem; }
.kind-select {
  padding: 0.5rem 0.75rem; border-radius: 0.625rem; border: 1px solid rgba(0, 0, 0, 0.08);
  background: rgba(255, 255, 255, 0.5); font-size: 0.8125rem; font-family: inherit; color: #1d1d1f; outline: none;
}
.btn-delete {
  display: flex; align-items: center; gap: 0.5rem; padding: 0.625rem 1.25rem; border-radius: 0.75rem;
  font-size: 0.8125rem; font-weight: 600; color: #ffffff;
  background: linear-gradient(135deg, #ff3b30 0%, #d63028 100%); border: none; cursor: pointer;
  transition: all 200ms ease; font-family: inherit; box-shadow: 0 1px 3px rgba(255, 59, 48, 0.25);
}
.btn-delete:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(255, 59, 48, 0.3); }
button:disabled { opacity: 0.5; cursor: not-allowed; }
.spin { animation: spin 0.8s linear infinite; }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
.api-info { display: flex; flex-direction: column; gap: 0.5rem; }
.api-row { display: flex; align-items: center; gap: 0.75rem; }
.api-key { font-size: 0.75rem; font-weight: 500; color: rgba(60, 60, 67, 0.5); min-width: 5.5rem; }
.api-val { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: #1d1d1f; background: rgba(0, 0, 0, 0.03); padding: 0.1875rem 0.5rem; border-radius: 0.375rem; }
</style>
