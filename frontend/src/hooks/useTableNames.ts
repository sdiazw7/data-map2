import { useState, useEffect, useCallback } from 'react'
import { getTableNames } from '../services/metadataService'

type UseTableNamesResult = {
  tableNames: string[]
  isLoading: boolean
  error: string | null
  reload: () => void
}

export function useTableNames(): UseTableNamesResult {
  const [tableNames, setTableNames] = useState<string[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  const reload = useCallback(() => setTick(t => t + 1), [])

  useEffect(() => {
    setIsLoading(true)
    setError(null)

    getTableNames()
      .then(setTableNames)
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : 'Failed to load tables.')
      })
      .finally(() => setIsLoading(false))
  }, [tick])

  return { tableNames, isLoading, error, reload }
}
