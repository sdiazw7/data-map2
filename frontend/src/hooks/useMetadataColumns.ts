import { useState, useEffect, useCallback } from 'react'
import type { ColumnGridRow } from '../types/api'
import type { ColumnsQuery } from '../services/metadataService'
import { getColumns } from '../services/metadataService'

type UseMetadataColumnsResult = {
  columns: ColumnGridRow[]
  isLoading: boolean
  error: string | null
  reload: () => void
}

export function useMetadataColumns(query: ColumnsQuery): UseMetadataColumnsResult {
  const [columns, setColumns] = useState<ColumnGridRow[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  const reload = useCallback(() => setTick(t => t + 1), [])

  useEffect(() => {
    setIsLoading(true)
    setError(null)

    getColumns(query)
      .then(setColumns)
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : 'Failed to load columns.')
      })
      .finally(() => setIsLoading(false))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query.search, query.undocumented_only, query.table_name, query.limit, query.offset, tick])

  return { columns, isLoading, error, reload }
}
