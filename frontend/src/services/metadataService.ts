import type {
  BulkUpdateResponse,
  ColumnGridRow,
  ColumnUpdateRequest,
  CoverageResponse,
  ImportSummary,
  PagedResult,
} from '../types/api'
import { apiFetch, toApiError } from '../utils/api'

/** Sort fields are the response field names, so one identifier does both jobs. */
export type SortField = 'columnName' | 'tableName' | 'dataType' | 'owner'
export type SortDir = 'asc' | 'desc'

export type ColumnsQuery = {
  limit?: number
  offset?: number
  search?: string
  undocumentedOnly?: boolean
  tableName?: string
  sortBy?: SortField
  sortDir?: SortDir
}

export async function getColumns(
  query: ColumnsQuery,
  signal?: AbortSignal,
): Promise<PagedResult<ColumnGridRow>> {
  const params = new URLSearchParams()
  if (query.limit !== undefined) params.set('limit', String(query.limit))
  if (query.offset !== undefined) params.set('offset', String(query.offset))
  if (query.search) params.set('search', query.search)
  if (query.undocumentedOnly) params.set('undocumentedOnly', 'true')
  if (query.tableName) params.set('tableName', query.tableName)
  if (query.sortBy) params.set('sortBy', query.sortBy)
  if (query.sortDir) params.set('sortDir', query.sortDir)
  return apiFetch<PagedResult<ColumnGridRow>>(`/columns?${params}`, { signal })
}

export async function getTableNames(limit = 500, offset = 0): Promise<PagedResult<string>> {
  return apiFetch<PagedResult<string>>(`/tables?limit=${limit}&offset=${offset}`)
}

/** Returns each edited column's new version, so the caller need not refetch to keep writing. */
export async function bulkUpdateColumns(updates: ColumnUpdateRequest[]): Promise<BulkUpdateResponse> {
  return apiFetch<BulkUpdateResponse>('/columns', {
    method: 'PATCH',
    body: JSON.stringify(updates),
  })
}

export async function importCsv(file: File): Promise<ImportSummary> {
  const BASE_URL = import.meta.env.VITE_API_BASE_URL
  const form = new FormData()
  form.append('file', file)

  // Not routed through apiFetch: that sets a JSON content type, and the browser has to pick
  // the multipart boundary itself.
  const res = await fetch(`${BASE_URL}/imports`, {
    method: 'POST',
    credentials: 'include',
    body: form,
  })
  if (!res.ok) {
    throw await toApiError(res)
  }
  return (await res.json()) as ImportSummary
}

export async function getCoverage(): Promise<CoverageResponse> {
  return apiFetch<CoverageResponse>('/coverage')
}
