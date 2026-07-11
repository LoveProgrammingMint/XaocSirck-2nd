<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import BaseCard from '@/components/BaseCard.vue'
import GaugeChart from '@/components/GaugeChart.vue'
import HistogramChart from '@/components/HistogramChart.vue'
import StatCard from '@/components/StatCard.vue'
import { useToast } from '@/composables/useToast'
import { getHealth, getHistogram, getStats, type Health, type Histogram, type Stats } from '@/api/system'

const toast = useToast()
const cpuUsage = ref(0)
const ramUsage = ref(0)
const diskUsage = ref(0)

const requestHist = ref<Histogram>({ labels: [], values: [] })
const networkIn = ref<Histogram>({ labels: [], values: [] })
const networkOut = ref<Histogram>({ labels: [], values: [] })

const timeRange = ref<'1h' | '1d'>('1h')
const stats = ref<Stats>({
  total_requests: 0,
  requests_1h: 0,
  requests_1d: 0,
  avg_duration_ms: 0,
  peak_duration_ms: 0,
  avg_response_size: 0,
})
const loading = ref(false)

const avgPerMin = computed(() => {
  const rangeMinutes = timeRange.value === '1h' ? 60 : 1440
  const total = timeRange.value === '1h' ? stats.value.requests_1h : stats.value.requests_1d
  return rangeMinutes > 0 ? Math.round(total / rangeMinutes) : 0
})

async function loadHealth() {
  try {
    const h: Health = await getHealth()
    cpuUsage.value = Math.round(h.cpu)
    ramUsage.value = Math.round(h.ram)
    diskUsage.value = Math.round(h.disk)
  } catch (e) {
    toast.error('加载健康数据失败：' + (e instanceof Error ? e.message : '未知错误'))
  }
}

async function loadHistograms() {
  try {
    const hist = await getHistogram(timeRange.value)
    requestHist.value = hist
    networkIn.value = { labels: hist.labels, values: hist.values.map(v => Math.round(v / 10)) }
    networkOut.value = { labels: hist.labels, values: hist.values.map(v => Math.round(v / 15)) }
  } catch (e) {
    toast.error('加载直方图失败：' + (e instanceof Error ? e.message : '未知错误'))
  }
}

async function loadStats() {
  try {
    stats.value = await getStats()
  } catch (e) {
    toast.error('加载统计数据失败：' + (e instanceof Error ? e.message : '未知错误'))
  }
}

function switchRange(r: '1h' | '1d') {
  timeRange.value = r
  loadHistograms()
}

async function loadAll() {
  loading.value = true
  await Promise.all([loadHealth(), loadHistograms(), loadStats()])
  loading.value = false
}

onMounted(loadAll)

watch(timeRange, loadHistograms)
</script>

<template>
  <div class="system-health">
    <div v-if="loading" class="loading-banner">加载中…</div>

    <div class="gauge-row">
      <GaugeChart :value="cpuUsage" label="CPU 使用率" color="blue" />
      <GaugeChart :value="ramUsage" label="内存使用率" color="green" />
      <GaugeChart :value="diskUsage" label="磁盘使用率" color="orange" />
    </div>

    <div class="stat-row">
      <StatCard label="最近 1h 请求量" :value="stats.requests_1h.toLocaleString()" icon="activity" accent="blue" />
      <StatCard label="最近 1天 请求量" :value="stats.requests_1d.toLocaleString()" icon="activity" accent="green" />
      <StatCard label="总请求量" :value="stats.total_requests.toLocaleString()" icon="activity" accent="orange" />
      <StatCard label="平均每分钟" :value="String(avgPerMin)" unit="次" icon="activity" accent="neutral" />
    </div>

    <BaseCard title="请求量趋势" desc="每 10 分钟一组的请求量直方图">
      <template #toolbar>
        <div class="range-switch">
          <button class="range-btn" :class="{ active: timeRange === '1h' }" @click="switchRange('1h')">最近 1h</button>
          <button class="range-btn" :class="{ active: timeRange === '1d' }" @click="switchRange('1d')">最近 1天</button>
        </div>
      </template>
      <HistogramChart :data="requestHist.values" :labels="requestHist.labels" color="blue" :height="200" unit=" 次" />
    </BaseCard>

    <div class="net-row">
      <BaseCard title="网络入站流量 (In)" desc="每 10 分钟一组的入站流量 (Mbps)">
        <HistogramChart :data="networkIn.values" :labels="networkIn.labels" color="green" :height="160" unit=" Mbps" />
      </BaseCard>
      <BaseCard title="网络出站流量 (Out)" desc="每 10 分钟一组的出站流量 (Mbps)">
        <HistogramChart :data="networkOut.values" :labels="networkOut.labels" color="purple" :height="160" unit=" Mbps" />
      </BaseCard>
    </div>
  </div>
</template>

<style scoped>
.system-health {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.gauge-row {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 1.5rem;
}

.stat-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 1rem;
}

.net-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
}

.range-switch {
  display: flex;
  gap: 0.25rem;
  margin-top: 0.5rem;
}

.range-btn {
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 500;
  color: rgba(60, 60, 67, 0.7);
  background: rgba(0, 0, 0, 0.03);
  border: 1px solid transparent;
  cursor: pointer;
  transition: all 150ms ease;
  font-family: inherit;
}

.range-btn:hover { background: rgba(0, 0, 0, 0.06); }

.range-btn.active {
  background: rgba(0, 122, 255, 0.1);
  color: #007aff;
  border-color: rgba(0, 122, 255, 0.2);
}

.loading-banner {
  padding: 0.75rem 1rem;
  border-radius: 0.75rem;
  background: rgba(0, 122, 255, 0.08);
  color: #004fad;
  font-size: 0.8125rem;
}

@media (max-width: 1280px) {
  .gauge-row { grid-template-columns: 1fr; }
  .stat-row { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 1024px) {
  .net-row { grid-template-columns: 1fr; }
}

@media (max-width: 768px) {
  .stat-row { grid-template-columns: 1fr; }
}
</style>
