import { useState, useEffect } from 'react'
import type { BusinessTermDto, BusinessTermCreateRequest } from '../types/api'
import { getBusinessTerms, createBusinessTerm } from '../services/businessTermService'

type UseBusinessTermsResult = {
  terms: BusinessTermDto[]
  isLoading: boolean
  error: string | null
  create: (req: BusinessTermCreateRequest) => Promise<void>
}

export function useBusinessTerms(): UseBusinessTermsResult {
  const [terms, setTerms] = useState<BusinessTermDto[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setIsLoading(true)
    setError(null)

    getBusinessTerms()
      .then(page => setTerms(page.items))
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : 'Failed to load business terms.')
      })
      .finally(() => setIsLoading(false))
  }, [])

  async function create(req: BusinessTermCreateRequest): Promise<void> {
    const newTerm = await createBusinessTerm(req)
    setTerms(prev => [...prev, newTerm])
  }

  return { terms, isLoading, error, create }
}
