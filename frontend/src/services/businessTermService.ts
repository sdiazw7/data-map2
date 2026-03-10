import type { BusinessTermDto, BusinessTermCreateRequest, TermMappingRequest } from '../types/api'
import { apiFetch } from '../utils/api'

export async function getBusinessTerms(): Promise<BusinessTermDto[]> {
  return apiFetch<BusinessTermDto[]>('/business-terms')
}

export async function createBusinessTerm(req: BusinessTermCreateRequest): Promise<BusinessTermDto> {
  return apiFetch<BusinessTermDto>('/business-terms', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export async function mapTermToColumn(req: TermMappingRequest): Promise<void> {
  return apiFetch<void>('/business-terms/map', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}
