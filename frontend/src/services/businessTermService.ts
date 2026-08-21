import type {
  BusinessTermCreateRequest,
  BusinessTermDto,
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

/** The mapping is a property of the column, so it is addressed on the column. */
export async function setColumnBusinessTerm(columnId: string, termId: string): Promise<void> {
  return apiFetch<void>(`/columns/${columnId}/business-term`, {
    method: 'PUT',
    body: JSON.stringify({ termId }),
  })
}

export async function clearColumnBusinessTerm(columnId: string): Promise<void> {
  return apiFetch<void>(`/columns/${columnId}/business-term`, {
    method: 'DELETE',
  })
}
