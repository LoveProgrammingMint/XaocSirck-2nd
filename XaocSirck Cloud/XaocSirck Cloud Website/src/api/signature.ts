import { request } from './client'

export async function querySignature(signature: string): Promise<number> {
  const res = await fetch(`/api/signature/query?signature=${encodeURIComponent(signature)}`)
  if (!res.ok) throw new Error(`query failed: ${res.status}`)
  const buf = await res.arrayBuffer()
  return new Uint8Array(buf)[0] ?? 2
}

export async function addSignature(signature: string): Promise<void> {
  return request('/api/signature/add', {
    method: 'POST',
    body: JSON.stringify({ signature }),
  })
}

export async function deleteSignature(signature: string): Promise<void> {
  return request('/api/signature/delete', {
    method: 'POST',
    body: JSON.stringify({ signature }),
  })
}

export interface SignatureEntry {
  signature: string
  operator_token: string
  created_at: string
}

export async function getSignatures(params: { signature?: string; limit?: number; offset?: number }): Promise<SignatureEntry[]> {
  const search = new URLSearchParams()
  if (params.signature) search.set('signature', params.signature)
  if (params.limit !== undefined) search.set('limit', String(params.limit))
  if (params.offset !== undefined) search.set('offset', String(params.offset))
  const query = search.toString()
  return request<SignatureEntry[]>(`/api/signature/get${query ? `?${query}` : ''}`)
}

export async function clearSignatures(): Promise<{ cleared: number }> {
  return request('/api/signature/clear', { method: 'POST' })
}
