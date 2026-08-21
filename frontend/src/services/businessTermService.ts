import type {
  BusinessTermCreateRequest,
  BusinessTermDto,
  ColumnVersion,
  PagedResult,
} from '../types/api'
import { apiFetch } from '../utils/api'

export async function getBusinessTerms(limit = 200, offset = 0): Promise<PagedResult<BusinessTermDto>> {
  return apiFetch<PagedResult<BusinessTermDto>>(`/business-terms?limit=${limit}&offset=${offset}`)
}

export async function getBusinessTerm(id: string): Promise<BusinessTermDto> {
  return apiFetch<BusinessTermDto>(`/business-terms/${id}`)
}

export async function createBusinessTerm(req: BusinessTermCreateRequest): Promise<BusinessTermDto> {
  return apiFetch<BusinessTermDto>('/business-terms', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

/**
 * The mapping is a property of the column, so it is addressed on the column. Returns the
 * column's new version: mapping a term moves the row's concurrency token, and a caller that
 * kept the old one would have its next edit to that row rejected as stale.
 */
export async function setColumnBusinessTerm(
  columnId: string,
  termId: string,
): Promise<ColumnVersion> {
  return apiFetch<ColumnVersion>(`/columns/${columnId}/business-term`, {
    method: 'PUT',
    body: JSON.stringify({ termId }),
  })
}

export async function clearColumnBusinessTerm(columnId: string): Promise<ColumnVersion> {
  return apiFetch<ColumnVersion>(`/columns/${columnId}/business-term`, {
    method: 'DELETE',
  })
}
