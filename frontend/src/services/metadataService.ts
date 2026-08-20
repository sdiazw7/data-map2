import type { ColumnGridRow, ColumnUpdateRequest, CoverageResponse } from '../types/api'
import { apiFetch } from '../utils/api'

export type ColumnsQuery = {
  limit?: number
  offset?: number
  search?: string
  undocumented_only?: boolean
  table_name?: string
}

export async function getColumns(query: ColumnsQuery): Promise<ColumnGridRow[]> {
  const params = new URLSearchParams()
  if (query.limit !== undefined) params.set('limit', String(query.limit))
  if (query.offset !== undefined) params.set('offset', String(query.offset))
  if (query.search) params.set('search', query.search)
  if (query.undocumented_only) params.set('undocumented_only', 'true')
  if (query.table_name) params.set('table_name', query.table_name)
  return apiFetch<ColumnGridRow[]>(`/metadata/columns?${params}`)
}

export async function getTableNames(): Promise<string[]> {
  return apiFetch<string[]>('/metadata/tables')
}

export async function bulkUpdateColumns(updates: ColumnUpdateRequest[]): Promise<void> {
  return apiFetch<void>('/metadata/columns', {
    method: 'PATCH',
    body: JSON.stringify(updates),
  })
}

export async function uploadCsv(file: File): Promise<void> {
  const BASE_URL = import.meta.env.VITE_API_BASE_URL
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${BASE_URL}/metadata/upload`, {
    method: 'POST',
    credentials: 'include',
    body: form,
  })
  if (!res.ok) {
    const body = await res.json().catch(() => null)
    throw new Error(body?.error?.message ?? `Upload failed: ${res.status}`)
  }
}

export async function getCoverage(): Promise<CoverageResponse> {
  return apiFetch<CoverageResponse>('/metadata/coverage')
}
