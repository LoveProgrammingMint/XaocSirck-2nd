import { request } from './client'

export async function queryCache(sha256: string): Promise<number> {
  const res = await fetch(`/api/cache/query?sha256=${sha256}`)
  if (!res.ok) throw new Error(`query failed: ${res.status}`)
  const buf = await res.arrayBuffer()
  return new Uint8Array(buf)[0] ?? 2
}

export async function addColdCache(sha256: string, label: number): Promise<void> {
  return request('/api/cache/add', {
    method: 'POST',
    body: JSON.stringify({ sha256, label }),
  })
}

export async function deleteColdCache(sha256: string): Promise<void> {
  return request('/api/cache/delete', {
    method: 'POST',
    body: JSON.stringify({ sha256 }),
  })
}

export interface ColdEntry {
  sha256: string
  label: number
  operator_token: string
  created_at: string
}

export async function getColdCache(params: { sha256?: string; limit?: number; offset?: number }): Promise<ColdEntry[]> {
  const search = new URLSearchParams()
  if (params.sha256) search.set('sha256', params.sha256)
  if (params.limit !== undefined) search.set('limit', String(params.limit))
  if (params.offset !== undefined) search.set('offset', String(params.offset))
  const query = search.toString()
  return request<ColdEntry[]>(`/api/cache/get${query ? `?${query}` : ''}`)
}

export async function clearColdCache(): Promise<{ cleared: number }> {
  return request('/api/cache/clear', { method: 'POST' })
}

export async function buildHotCache(): Promise<{ malicious: number; clean: number }> {
  return request('/api/cache/build', { method: 'POST' })
}

export async function deleteHotCache(kind?: 'malicious' | 'clean'): Promise<void> {
  return request('/api/cache/hot/delete', {
    method: 'POST',
    body: JSON.stringify({ kind }),
  })
}

export async function stopService(): Promise<string> {
  return request('/api/cache/stop', { method: 'POST' })
}

export async function restartService(): Promise<string> {
  return request('/api/cache/restart', { method: 'POST' })
}
