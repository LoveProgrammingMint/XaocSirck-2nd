<script setup lang="ts">
import { ref } from 'vue'
import BaseCard from '@/components/BaseCard.vue'
import { updateHotCache } from '@/api/cache'
import { useToast } from '@/composables/useToast'

const toast = useToast()

const selectedKind = ref<'malicious' | 'clean'>('malicious')
const fileName = ref('')
const selectedFile = ref<File | null>(null)
const uploadStatus = ref<'idle' | 'uploading' | 'success' | 'error'>('idle')
const isDragging = ref(false)

function onFileChange(e: Event) {
  const target = e.target as HTMLInputElement
  if (target.files && target.files[0]) {
    selectedFile.value = target.files[0]
    fileName.value = target.files[0].name
  }
}

function onDrop(e: DragEvent) {
  e.preventDefault()
  isDragging.value = false
  if (e.dataTransfer?.files && e.dataTransfer.files[0]) {
    selectedFile.value = e.dataTransfer.files[0]
    fileName.value = e.dataTransfer.files[0].name
  }
}

function clearFile() {
  fileName.value = ''
  selectedFile.value = null
  uploadStatus.value = 'idle'
}

async function doUpload() {
  if (!selectedFile.value) return
  uploadStatus.value = 'uploading'
  try {
    await updateHotCache(selectedKind.value, selectedFile.value)
    uploadStatus.value = 'success'
    toast.success(`${selectedKind.value === 'malicious' ? 'HotMal' : 'HotCle'} 上传成功，内存 MPHF 已替换`)
    setTimeout(() => {
      uploadStatus.value = 'idle'
      clearFile()
    }, 2000)
  } catch (e) {
    uploadStatus.value = 'error'
    toast.error('上传失败：' + (e as Error).message)
    setTimeout(() => { uploadStatus.value = 'idle' }, 2000)
  }
}
</script>

<template>
  <div class="hot-cache">
    <BaseCard title="热缓存 MPHF 上传" desc="上传 HotMal.mphf 或 HotCle.mphf，上传成功后自动替换内存中的 MPHF">
      <div class="upload-area">
        <div class="kind-selector">
          <button class="kind-btn" :class="{ active: selectedKind === 'malicious' }" @click="selectedKind = 'malicious'">
            <div class="kind-dot red"></div>
            <div class="kind-info">
              <span class="kind-name">HotMal.mphf</span>
              <span class="kind-desc">恶意文件哈希热表</span>
            </div>
          </button>
          <button class="kind-btn" :class="{ active: selectedKind === 'clean' }" @click="selectedKind = 'clean'">
            <div class="kind-dot green"></div>
            <div class="kind-info">
              <span class="kind-name">HotCle.mphf</span>
              <span class="kind-desc">干净文件哈希热表</span>
            </div>
          </button>
        </div>

        <div class="dropzone" :class="{ dragging: isDragging, hasFile: !!fileName }" @dragover.prevent="isDragging = true" @dragleave.prevent="isDragging = false" @drop="onDrop">
          <input type="file" accept=".mphf" class="file-input" @change="onFileChange" />
          <div v-if="!fileName" class="dropzone-empty">
            <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><polyline points="17 8 12 3 7 8" /><line x1="12" x2="12" y1="3" y2="15" />
            </svg>
            <span class="drop-text">拖拽 .mphf 文件到此处</span>
            <span class="drop-sub">或点击选择文件</span>
          </div>
          <div v-else class="dropzone-file">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" /><polyline points="14 2 14 8 20 8" />
            </svg>
            <span class="file-name">{{ fileName }}</span>
            <button class="btn-clear" @click="clearFile">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M18 6 6 18" /><path d="m6 6 12 12" />
              </svg>
            </button>
          </div>
        </div>

        <div class="upload-actions">
          <button class="btn-upload" :disabled="!fileName || uploadStatus === 'uploading'" @click="doUpload">
            <svg v-if="uploadStatus === 'uploading'" class="spin" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-6.219-8.56" /></svg>
            <svg v-else-if="uploadStatus === 'success'" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6 9 17l-5-5" /></svg>
            <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><polyline points="17 8 12 3 7 8" /><line x1="12" x2="12" y1="3" y2="15" /></svg>
            {{ uploadStatus === 'uploading' ? '上传中…' : uploadStatus === 'success' ? '上传成功' : uploadStatus === 'error' ? '上传失败' : '上传并替换' }}
          </button>
        </div>
      </div>
    </BaseCard>

    <BaseCard title="接口信息" desc="POST /api/cache/update">
      <div class="api-info">
        <div class="api-row"><span class="api-key">路径</span><code class="api-val">/api/cache/update</code></div>
        <div class="api-row"><span class="api-key">方法</span><code class="api-val">POST</code></div>
        <div class="api-row"><span class="api-key">权限</span><span class="api-val auth">需 Token (Bearer JWT)</span></div>
        <div class="api-row"><span class="api-key">Content-Type</span><code class="api-val">multipart/form-data</code></div>
        <div class="api-row"><span class="api-key">字段 kind</span><code class="api-val">malicious | clean</code></div>
        <div class="api-row"><span class="api-key">字段 file</span><code class="api-val">.mphf 二进制文件</code></div>
        <div class="api-row"><span class="api-key">成功响应</span><code class="api-val">200 OK: updated</code></div>
        <div class="api-row"><span class="api-key">说明</span><span class="api-val">覆盖 ./MPHFs/ 下对应文件并重新加载到内存</span></div>
      </div>
    </BaseCard>
  </div>
</template>

<style scoped>
.hot-cache { display: flex; flex-direction: column; gap: 1.5rem; }
.upload-area { display: flex; flex-direction: column; gap: 1rem; }
.kind-selector { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.kind-btn { display: flex; align-items: center; gap: 0.625rem; padding: 0.75rem; border-radius: 0.75rem; border: 2px solid rgba(0, 0, 0, 0.06); background: rgba(255, 255, 255, 0.4); cursor: pointer; transition: all 200ms ease; font-family: inherit; text-align: left; }
.kind-btn:hover { border-color: rgba(0, 122, 255, 0.2); }
.kind-btn.active { border-color: #007aff; background: rgba(0, 122, 255, 0.04); }
.kind-dot { width: 0.625rem; height: 0.625rem; border-radius: 50%; flex-shrink: 0; }
.kind-dot.red { background: #ff3b30; }
.kind-dot.green { background: #34c759; }
.kind-info { display: flex; flex-direction: column; }
.kind-name { font-size: 0.8125rem; font-weight: 600; font-family: 'JetBrains Mono', monospace; color: #1d1d1f; }
.kind-desc { font-size: 0.6875rem; color: rgba(60, 60, 67, 0.5); }
.dropzone { position: relative; border: 2px dashed rgba(0, 0, 0, 0.1); border-radius: 1rem; padding: 2rem; display: flex; align-items: center; justify-content: center; transition: all 200ms ease; background: rgba(0, 0, 0, 0.01); min-height: 8rem; }
.dropzone.dragging { border-color: #007aff; background: rgba(0, 122, 255, 0.04); }
.dropzone.hasFile { border-style: solid; border-color: rgba(0, 122, 255, 0.2); background: rgba(0, 122, 255, 0.02); }
.file-input { position: absolute; inset: 0; opacity: 0; cursor: pointer; z-index: 1; }
.dropzone-empty { display: flex; flex-direction: column; align-items: center; gap: 0.5rem; color: rgba(60, 60, 67, 0.4); }
.drop-text { font-size: 0.875rem; font-weight: 500; color: rgba(60, 60, 67, 0.6); }
.drop-sub { font-size: 0.75rem; color: rgba(60, 60, 67, 0.4); }
.dropzone-file { display: flex; align-items: center; gap: 0.625rem; color: #007aff; }
.file-name { font-family: 'JetBrains Mono', monospace; font-size: 0.8125rem; font-weight: 500; color: #1d1d1f; }
.btn-clear { display: flex; align-items: center; justify-content: center; width: 1.5rem; height: 1.5rem; border-radius: 50%; border: none; background: rgba(0, 0, 0, 0.06); color: rgba(60, 60, 67, 0.5); cursor: pointer; transition: all 150ms ease; }
.btn-clear:hover { background: rgba(255, 59, 48, 0.1); color: #ff3b30; }
.upload-actions { display: flex; justify-content: flex-end; }
.btn-upload { display: flex; align-items: center; gap: 0.5rem; padding: 0.625rem 1.5rem; border-radius: 0.75rem; font-size: 0.8125rem; font-weight: 600; color: #ffffff; background: linear-gradient(135deg, #007aff 0%, #0064d6 100%); border: none; cursor: pointer; transition: all 200ms ease; font-family: inherit; box-shadow: 0 1px 3px rgba(0, 122, 255, 0.25); }
.btn-upload:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3); }
.btn-upload:disabled { opacity: 0.5; cursor: not-allowed; }
.spin { animation: spin 0.8s linear infinite; }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
.api-info { display: flex; flex-direction: column; gap: 0.5rem; }
.api-row { display: flex; align-items: center; gap: 0.75rem; }
.api-key { font-size: 0.75rem; font-weight: 500; color: rgba(60, 60, 67, 0.5); min-width: 5.5rem; }
.api-val { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; color: #1d1d1f; background: rgba(0, 0, 0, 0.03); padding: 0.1875rem 0.5rem; border-radius: 0.375rem; }
.api-val.auth { color: #c2750a; background: rgba(255, 159, 10, 0.08); }
@media (max-width: 768px) { .kind-selector { grid-template-columns: 1fr; } }
</style>
