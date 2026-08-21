import { useState, useEffect, useCallback } from 'react'
import type { ColumnGridRow, ColumnUpdateRequest, ColumnVersion } from '../types/api'
import type { ColumnsQuery } from '../services/metadataService'
import { getColumns } from '../services/metadataService'

type UseMetadataColumnsResult = {
  columns: ColumnGridRow[]
  /** Rows matching the current filters across all pages, not just the ones loaded. */
  total: number
  isLoading: boolean
  error: string | null
  reload: () => void
  /** Applies an edit the server has already accepted, without refetching the page. */
  applyUpdate: (update: ColumnUpdateRequest, versions: ColumnVersion[]) => void
  /** Applies a term change the server has already accepted. */
  applyTerm: (columnId: string, termName: string | null) => void
}

export function useMetadataColumns(query: ColumnsQuery): UseMetadataColumnsResult {
  const [columns, setColumns] = useState<ColumnGridRow[]>([])
  const [total, setTotal] = useState(0)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  const reload = useCallback(() => setTick(t => t + 1), [])

  const applyUpdate = useCallback((update: ColumnUpdateRequest, versions: ColumnVersion[]) => {
    // The server returns the new version per column, so the edited row can be reconciled in
    // place. Refetching the whole page after every keystroke-level edit was only ever a way
    // to recover numbers the response now carries.
    const versionById = new Map(versions.map(v => [v.columnId, v.version]))

    setColumns(prev =>
      prev.map(row =>
        row.columnId === update.columnId
          ? {
              ...row,
              description: update.description,
              exampleValue: update.exampleValue,
              owner: update.owner,
              version: versionById.get(row.columnId) ?? row.version,
            }
          : row,
      ),
    )
  }, [])

  const applyTerm = useCallback((columnId: string, termName: string | null) => {
    setColumns(prev =>
      prev.map(row => (row.columnId === columnId ? { ...row, businessTerm: termName } : row)),
    )
  }, [])

  useEffect(() => {
    setIsLoading(true)
    setError(null)

    getColumns(query)
      .then(page => {
        setColumns(page.items)
        setTotal(page.total)
      })
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : 'Failed to load columns.')
      })
      .finally(() => setIsLoading(false))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query.search, query.undocumentedOnly, query.tableName, query.sortBy, query.sortDir, query.limit, query.offset, tick])

  return { columns, total, isLoading, error, reload, applyUpdate, applyTerm }
}
