import { request } from './client'

export interface Stats {
  total_requests: number
  requests_1h: number
  requests_1d: number
  avg_duration_ms: number
  peak_duration_ms: number
  avg_response_size: number
}

export async function getStats(): Promise<Stats> {
  return request('/api/system/stats')
}

export interface Health {
  cpu: number
  ram: number
  disk: number
}

export async function getHealth(): Promise<Health> {
  return request('/api/system/health')
}

export interface Histogram {
  labels: string[]
  values: number[]
}

export async function getHistogram(range: '1h' | '1d'): Promise<Histogram> {
  return request(`/api/system/histogram?range=${range}`)
}

export interface RouteStat {
  route: string
  method: string
  calls: number
  avg_ms: number
}

export async function getRouteStats(): Promise<RouteStat[]> {
  return request('/api/system/routes')
}

export interface ErrorLog {
  id: number
  time: string
  subsystem: string
  level: 'error' | 'warning' | 'info'
  message: string
  callback?: string
  forwarded: boolean
}

export async function getErrors(params?: { subsystem?: string; level?: string; limit?: number; offset?: number }): Promise<ErrorLog[]> {
  const search = new URLSearchParams()
  if (params?.subsystem) search.set('subsystem', params.subsystem)
  if (params?.level) search.set('level', params.level)
  if (params?.limit !== undefined) search.set('limit', String(params.limit))
  if (params?.offset !== undefined) search.set('offset', String(params.offset))
  const query = search.toString()
  return request(`/api/system/errors${query ? `?${query}` : ''}`)
}

export async function createError(body: { subsystem: string; level: string; message: string; callback?: string }): Promise<void> {
  return request('/api/system/errors', { method: 'POST', body: JSON.stringify(body) })
}

export interface IPRecord {
  ip: string
  requests: number
  last_access?: string
  status: 'normal' | 'blocked' | 'rate-limited'
}

export async function getIPStats(params?: { limit?: number; offset?: number }): Promise<IPRecord[]> {
  const search = new URLSearchParams()
  if (params?.limit !== undefined) search.set('limit', String(params.limit))
  if (params?.offset !== undefined) search.set('offset', String(params.offset))
  const query = search.toString()
  return request(`/api/system/ips${query ? `?${query}` : ''}`)
}

export interface BlacklistEntry {
  ip: string
  reason: string
  created_at: string
}

export async function getBlacklist(): Promise<BlacklistEntry[]> {
  return request('/api/system/blacklist')
}

export async function addBlacklist(ip: string, reason: string): Promise<void> {
  return request('/api/system/blacklist', {
    method: 'POST',
    body: JSON.stringify({ ip, reason }),
  })
}

export async function removeBlacklist(ip: string): Promise<void> {
  return request(`/api/system/blacklist/remove?ip=${encodeURIComponent(ip)}`, { method: 'DELETE' })
}
