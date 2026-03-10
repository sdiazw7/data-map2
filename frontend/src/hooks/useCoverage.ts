import { useState, useEffect, useCallback } from 'react'
import type { CoverageResponse } from '../types/api'
import { getCoverage } from '../services/metadataService'

type UseCoverageResult = {
  coverage: CoverageResponse | null
  isLoading: boolean
  reload: () => void
}

export function useCoverage(): UseCoverageResult {
  const [coverage, setCoverage] = useState<CoverageResponse | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [tick, setTick] = useState(0)

  const reload = useCallback(() => setTick(t => t + 1), [])

  useEffect(() => {
    setIsLoading(true)

    getCoverage()
      .then(setCoverage)
      .catch(() => {
        // Coverage is non-critical; silently fail
      })
      .finally(() => setIsLoading(false))
  }, [tick])

  return { coverage, isLoading, reload }
}
