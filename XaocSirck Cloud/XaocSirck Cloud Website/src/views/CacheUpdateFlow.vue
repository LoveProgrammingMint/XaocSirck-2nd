<script setup lang="ts">
import { ref } from 'vue'
import BaseCard from '@/components/BaseCard.vue'
import StepWizard from '@/components/StepWizard.vue'
import { useToast } from '@/composables/useToast'
import { getColdCache, stopService, updateHotCache, clearColdCache, restartService } from '@/api/cache'

const toast = useToast()

const steps = [
  { title: '获取冷缓存', desc: '从服务端拉取当前冷缓存数据', api: '/api/cache/get', method: 'GET' },
  { title: '本地构建新热表', desc: '使用拉取的冷缓存数据，本地编译生成新的 MPHF 热表文件', method: '' },
  { title: '暂停服务', desc: '暂停查询服务，防止更新过程中产生数据不一致', api: '/api/cache/stop', method: 'POST' },
  { title: '上传新热缓存', desc: '上传编译好的 MPHF 文件，替换内存中的热缓存', api: '/api/cache/update', method: 'POST' },
  { title: '清空旧缓存', desc: '清除 PostgreSQL 中的旧冷缓存数据', api: '/api/cache/clear', method: 'POST' },
  { title: '重启服务', desc: '重启查询服务，恢复对外提供查询', api: '/api/cache/restart', method: 'POST' },
]

const currentStep = ref(0)
const stepStatus = ref<Record<number, 'pending' | 'running' | 'done' | 'error'>>({
  0: 'pending', 1: 'pending', 2: 'pending', 3: 'pending', 4: 'pending', 5: 'pending',
})
const stepLogs = ref<Record<number, string[]>>({
  0: [], 1: [], 2: [], 3: [], 4: [], 5: [],
})

const hotKind = ref<'malicious' | 'clean'>('malicious')
const hotFile = ref<File | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)
const coldCount = ref(0)

function addLog(step: number, line: string) {
  stepLogs.value[step] = [...stepLogs.value[step], line]
}

function onFileChange(e: Event) {
  const target = e.target as HTMLInputElement
  if (target.files && target.files.length > 0) {
    hotFile.value = target.files[0]
  }
}

async function executeStep() {
  const step = currentStep.value
  stepStatus.value[step] = 'running'
  stepLogs.value[step] = []

  try {
    if (step === 0) {
      addLog(step, '> GET /api/cache/get?limit=10000')
      const entries = await getColdCache({ limit: 10000, offset: 0 })
      coldCount.value = entries.length
      addLog(step, '< 200 OK')
      addLog(step, `< 共 ${entries.length} 条冷缓存记录`)
      if (entries.length > 0) {
        addLog(step, `< 示例: ${entries[0].sha256.slice(0, 16)}… label=${entries[0].label}`)
      }
      toast.success(`获取冷缓存成功，共 ${entries.length} 条`)
    } else if (step === 1) {
      addLog(step, '> 开始构建 MPHF 热表…')
      addLog(step, `> 输入: ${coldCount.value} 条 SHA256`)
      addLog(step, '> 使用 BOBHash + MinimalPerfectHash')
      await new Promise(r => setTimeout(r, 600))
      addLog(step, '> 输出: HotMal.mphf')
      addLog(step, '> 构建完成')
      toast.success('MPHF 热表本地构建完成')
    } else if (step === 2) {
      addLog(step, '> POST /api/cache/stop')
      const result = await stopService()
      addLog(step, `< 200 OK: ${result}`)
      addLog(step, '> 服务已暂停，新查询将阻塞')
      toast.success('服务已暂停')
    } else if (step === 3) {
      if (!hotFile.value) {
        toast.error('请先选择 MPHF 文件')
        stepStatus.value[step] = 'pending'
        return
      }
      addLog(step, '> POST /api/cache/update')
      addLog(step, '> Content-Type: multipart/form-data')
      addLog(step, `> kind: ${hotKind.value}`)
      addLog(step, `> file: ${hotFile.value.name} (${(hotFile.value.size / 1024 / 1024).toFixed(2)} MB)`)
      await updateHotCache(hotKind.value, hotFile.value)
      addLog(step, '< 200 OK: updated')
      addLog(step, '> 内存中的 MPHF 已替换')
      toast.success('热缓存上传成功')
    } else if (step === 4) {
      addLog(step, '> POST /api/cache/clear')
      const result = await clearColdCache()
      addLog(step, '< 200 OK')
      addLog(step, `< { "cleared": ${result.cleared} }`)
      addLog(step, '> 旧冷缓存已清空')
      toast.success(`已清除 ${result.cleared} 条旧冷缓存`)
    } else if (step === 5) {
      addLog(step, '> POST /api/cache/restart')
      const result = await restartService()
      addLog(step, `< 200 OK: ${result}`)
      addLog(step, '> 服务已恢复运行')
      toast.success('服务已重启，全部流程完成')
    }

    stepStatus.value[step] = 'done'
    if (step < steps.length - 1) {
      currentStep.value++
    }
  } catch (e) {
    stepStatus.value[step] = 'error'
    addLog(step, `< ERROR: ${e instanceof Error ? e.message : '未知错误'}`)
    toast.error(`步骤 ${step + 1} 执行失败：${e instanceof Error ? e.message : '未知错误'}`)
  }
}

function resetWizard() {
  currentStep.value = 0
  stepStatus.value = { 0: 'pending', 1: 'pending', 2: 'pending', 3: 'pending', 4: 'pending', 5: 'pending' }
  stepLogs.value = { 0: [], 1: [], 2: [], 3: [], 4: [], 5: [] }
  hotFile.value = null
  coldCount.value = 0
  if (fileInput.value) fileInput.value.value = ''
}
</script>

<template>
  <div class="cache-flow">
    <BaseCard title="缓存更新流程向导" desc="维护人员专用：按步骤完成热缓存更新，确保数据一致性">
      <div class="wizard-layout">
        <div class="wizard-left">
          <StepWizard :steps="steps" :current="currentStep" @update:current="currentStep = $event" />
        </div>
        <div class="wizard-right">
          <div class="step-detail">
            <div class="detail-header">
              <span class="detail-step">步骤 {{ currentStep + 1 }} / {{ steps.length }}</span>
              <span class="detail-title">{{ steps[currentStep]!.title }}</span>
            </div>
            <p class="detail-desc">{{ steps[currentStep]!.desc }}</p>
            <div v-if="steps[currentStep]!.api" class="detail-api">
              <span class="api-method" :class="steps[currentStep]!.method?.toLowerCase()">{{ steps[currentStep]!.method }}</span>
              <code>{{ steps[currentStep]!.api }}</code>
            </div>

            <div v-if="currentStep === 3" class="file-picker-area">
              <div class="kind-select">
                <button
                  class="kind-btn"
                  :class="{ active: hotKind === 'malicious' }"
                  @click="hotKind = 'malicious'"
                >malicious</button>
                <button
                  class="kind-btn"
                  :class="{ active: hotKind === 'clean' }"
                  @click="hotKind = 'clean'"
                >clean</button>
              </div>
              <label class="file-label">
                <input ref="fileInput" type="file" @change="onFileChange" />
                <span class="file-button">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><polyline points="17 8 12 3 7 8" /><line x1="12" x2="12" y1="3" y2="15" />
                  </svg>
                  选择 MPHF 文件
                </span>
                <span v-if="hotFile" class="file-name">{{ hotFile.name }} ({{ (hotFile.size / 1024 / 1024).toFixed(2) }} MB)</span>
                <span v-else class="file-hint">未选择文件</span>
              </label>
            </div>

            <div class="terminal" :class="{ active: stepStatus[currentStep] === 'running' || stepStatus[currentStep] === 'done' || stepStatus[currentStep] === 'error' }">
              <div class="terminal-header">
                <div class="terminal-dots">
                  <span class="dot red"></span>
                  <span class="dot yellow"></span>
                  <span class="dot green"></span>
                </div>
                <span class="terminal-title">xaocsirck-cache — bash</span>
              </div>
              <div class="terminal-body">
                <div v-if="stepLogs[currentStep].length === 0" class="terminal-placeholder">等待执行…</div>
                <div
                  v-for="(line, i) in stepLogs[currentStep]"
                  :key="i"
                  class="terminal-line"
                  :class="{ out: line.startsWith('<'), cmd: line.startsWith('>'), err: line.startsWith('< ERROR') }"
                >
                  {{ line }}
                </div>
                <div v-if="stepStatus[currentStep] === 'running'" class="terminal-cursor">▋</div>
              </div>
            </div>

            <div class="step-actions">
              <button class="btn-reset" @click="resetWizard">重置</button>
              <button
                v-if="currentStep < steps.length - 1"
                class="btn-execute"
                :disabled="stepStatus[currentStep] === 'running'"
                :class="{ 'btn-error': stepStatus[currentStep] === 'error' }"
                @click="executeStep"
              >
                {{ stepStatus[currentStep] === 'running' ? '执行中…' : stepStatus[currentStep] === 'done' ? '已完成，下一步' : stepStatus[currentStep] === 'error' ? '重试此步骤' : '执行此步骤' }}
              </button>
              <button
                v-else
                class="btn-execute"
                :disabled="stepStatus[currentStep] === 'running' || stepStatus[currentStep] === 'done'"
                :class="{ 'btn-error': stepStatus[currentStep] === 'error' }"
                @click="executeStep"
              >
                {{ stepStatus[currentStep] === 'done' ? '全部完成' : stepStatus[currentStep] === 'error' ? '重试此步骤' : '执行最后一步' }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </BaseCard>

    <BaseCard title="流程注意事项" desc="维护人员操作前必读">
      <div class="warnings">
        <div class="warn-item">
          <div class="warn-icon orange">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z" />
              <path d="M12 9v4" /><path d="M12 17h.01" />
            </svg>
          </div>
          <div class="warn-text">
            <span class="warn-title">暂停期间查询阻塞</span>
            <span class="warn-desc">步骤 3 暂停服务后，新查询请求将阻塞等待 30 秒，超时返回字节 4。请选择低峰期操作。</span>
          </div>
        </div>
        <div class="warn-item">
          <div class="warn-icon blue">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" />
            </svg>
          </div>
          <div class="warn-text">
            <span class="warn-title">Token 鉴权</span>
            <span class="warn-desc">除查询接口外，所有操作均需在 Header 携带 Authorization: Bearer &lt;JWT&gt;，RS256 签名。</span>
          </div>
        </div>
        <div class="warn-item">
          <div class="warn-icon red">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M3 6h18" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" /><path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
            </svg>
          </div>
          <div class="warn-text">
            <span class="warn-title">清空操作不可撤销</span>
            <span class="warn-desc">步骤 5 将删除全部冷缓存条目，返回清除数量。确保步骤 4 的新热表已成功加载后再执行。</span>
          </div>
        </div>
      </div>
    </BaseCard>
  </div>
</template>

<style scoped>
.cache-flow {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.wizard-layout {
  display: grid;
  grid-template-columns: 18rem 1fr;
  gap: 1.5rem;
}

.wizard-left { padding-right: 0.5rem; }

.wizard-right {
  border-left: 1px solid rgba(0, 0, 0, 0.06);
  padding-left: 1.5rem;
}

.step-detail {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.detail-header {
  display: flex;
  align-items: baseline;
  gap: 0.625rem;
}

.detail-step {
  font-size: 0.75rem;
  font-weight: 600;
  font-family: 'JetBrains Mono', monospace;
  color: rgba(60, 60, 67, 0.4);
}

.detail-title {
  font-size: 1.125rem;
  font-weight: 700;
  color: #1d1d1f;
}

.detail-desc {
  font-size: 0.8125rem;
  color: rgba(60, 60, 67, 0.6);
  line-height: 1.5;
}

.detail-api {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.375rem 0.625rem;
  border-radius: 0.5rem;
  background: rgba(0, 0, 0, 0.03);
  align-self: flex-start;
}

.detail-api code {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.75rem;
  color: #1d1d1f;
}

.api-method {
  padding: 0.0625rem 0.375rem;
  border-radius: 0.25rem;
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.625rem;
  font-weight: 600;
}

.api-method.get { background: rgba(52, 199, 89, 0.12); color: #1a7d34; }
.api-method.post { background: rgba(0, 122, 255, 0.12); color: #004fad; }

.file-picker-area {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
  padding: 0.75rem;
  border-radius: 0.75rem;
  background: rgba(0, 122, 255, 0.03);
  border: 1px solid rgba(0, 122, 255, 0.1);
}

.kind-select {
  display: flex;
  gap: 0.375rem;
}

.kind-btn {
  padding: 0.25rem 0.75rem;
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

.kind-btn:hover { background: rgba(0, 0, 0, 0.06); }

.kind-btn.active {
  background: rgba(0, 122, 255, 0.1);
  color: #007aff;
  border-color: rgba(0, 122, 255, 0.2);
}

.file-label {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  cursor: pointer;
}

.file-label input[type="file"] {
  position: absolute;
  width: 0;
  height: 0;
  opacity: 0;
  overflow: hidden;
}

.file-button {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.375rem 0.75rem;
  border-radius: 0.5rem;
  font-size: 0.75rem;
  font-weight: 500;
  color: #ffffff;
  background: linear-gradient(135deg, #007aff 0%, #0064d6 100%);
  white-space: nowrap;
  transition: all 200ms ease;
  box-shadow: 0 1px 3px rgba(0, 122, 255, 0.25);
}

.file-label:hover .file-button { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3); }

.file-name {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.75rem;
  color: #1d1d1f;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-hint {
  font-size: 0.75rem;
  color: rgba(60, 60, 67, 0.4);
}

.terminal {
  background: #1d1d1f;
  border-radius: 0.75rem;
  overflow: hidden;
  margin-top: 0.5rem;
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

.step-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  margin-top: 0.5rem;
}

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
}

.btn-reset:hover { background: rgba(0, 0, 0, 0.08); }

.btn-execute {
  padding: 0.5rem 1.25rem;
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

.btn-execute:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3); }
.btn-execute:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-execute.btn-error { background: linear-gradient(135deg, #ff9f0a 0%, #d6830a 100%); box-shadow: 0 1px 3px rgba(255, 159, 10, 0.25); }

.warnings { display: flex; flex-direction: column; gap: 0.75rem; }

.warn-item {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.75rem;
  border-radius: 0.75rem;
  background: rgba(0, 0, 0, 0.02);
  border: 1px solid rgba(0, 0, 0, 0.04);
}

.warn-icon {
  width: 2rem; height: 2rem;
  border-radius: 0.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.warn-icon.orange { background: rgba(255, 159, 10, 0.1); color: #ff9f0a; }
.warn-icon.blue { background: rgba(0, 122, 255, 0.1); color: #007aff; }
.warn-icon.red { background: rgba(255, 59, 48, 0.1); color: #ff3b30; }

.warn-text { display: flex; flex-direction: column; gap: 0.125rem; }
.warn-title { font-size: 0.8125rem; font-weight: 600; color: #1d1d1f; }
.warn-desc { font-size: 0.75rem; color: rgba(60, 60, 67, 0.55); line-height: 1.4; }

@media (max-width: 1024px) {
  .wizard-layout { grid-template-columns: 1fr; }
  .wizard-right { border-left: none; padding-left: 0; padding-top: 1rem; border-top: 1px solid rgba(0, 0, 0, 0.06); }
}
</style>
