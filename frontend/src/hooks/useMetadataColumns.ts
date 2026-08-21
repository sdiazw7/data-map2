import { useState, useEffect, useCallback, useRef } from 'react'
import type { ColumnGridRow, ColumnUpdateRequest } from '../types/api'
import type { ColumnsQuery } from '../services/metadataService'
import { getColumns, bulkUpdateColumns } from '../services/metadataService'
import { ApiError, ApiErrorCode } from '../utils/api'

/** The editable fields of a row. A cell sends only the one it changed. */
export type ColumnEdit = Partial<Pick<ColumnGridRow, 'description' | 'exampleValue' | 'owner'>>

type UseMetadataColumnsResult = {
  columns: ColumnGridRow[]
  /** Rows matching the current filters across all pages, not just the ones loaded. */
  total: number
  isLoading: boolean
  error: string | null
  reload: () => void
  /**
   * Shows the edit immediately, then confirms it against the server. Rejects with the
   * {@link ApiError} if the write failed, having already put the row back as it was.
   */
  editColumn: (columnId: string, edit: ColumnEdit) => Promise<void>
  /** Sets a row's business term locally, returning the term it replaced so a failed write can undo it. */
  applyTerm: (columnId: string, termName: string | null) => string | null
}

export function useMetadataColumns(query: ColumnsQuery): UseMetadataColumnsResult {
  const [columns, setColumns] = useState<ColumnGridRow[]>([])
  const [total, setTotal] = useState(0)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  // An edit has to read the row it is editing — for its current version, and for the values to
  // restore if the write fails — and React state is a render behind by the time a handler runs.
  // Every write to `columns` goes through setRows so the two never diverge.
  const rowsRef = useRef<ColumnGridRow[]>([])

  const setRows = useCallback(
    (next: ColumnGridRow[] | ((prev: ColumnGridRow[]) => ColumnGridRow[])) => {
      const value = typeof next === 'function' ? next(rowsRef.current) : next
      rowsRef.current = value
      setColumns(value)
    },
    [],
  )

  const patchRow = useCallback(
    (columnId: string, patch: Partial<ColumnGridRow>) => {
      setRows(rows => rows.map(row => (row.columnId === columnId ? { ...row, ...patch } : row)))
    },
    [setRows],
  )

  const reload = useCallback(() => setTick(t => t + 1), [])

  const editColumn = useCallback(
    async (columnId: string, edit: ColumnEdit) => {
      const before = rowsRef.current.find(row => row.columnId === columnId)
      if (!before) return

      // Optimistic: the cell shows the new value now. Everything below either confirms it or
      // takes it back.
      patchRow(columnId, edit)

      // The version travels with the request as the concurrency token. It comes from the row
      // as last confirmed by the server, never from what the grid is currently showing.
      const after = { ...before, ...edit }
      const request: ColumnUpdateRequest = {
        columnId,
        description: after.description,
        exampleValue: after.exampleValue,
        owner: after.owner,
        version: before.version,
      }

      try {
        const result = await bulkUpdateColumns([request])

        // The response carries the new version, so the row is reconciled in place. Without it
        // the row would keep the version it just spent and every later edit would conflict.
        const version = result.columns.find(c => c.columnId === columnId)?.version
        if (version !== undefined) patchRow(columnId, { version })
      } catch (err: unknown) {
        // Undo only the fields this edit touched, so a term mapping that landed on the same
        // row in the meantime survives the rollback.
        const revert: ColumnEdit = {}
        if ('description' in edit) revert.description = before.description
        if ('exampleValue' in edit) revert.exampleValue = before.exampleValue
        if ('owner' in edit) revert.owner = before.owner
        patchRow(columnId, revert)

        // Someone else wrote to this row first. Rolling back restores what we last saw, which
        // is already out of date — only a reload gets the winning values and a usable version.
        if (err instanceof ApiError && err.code === ApiErrorCode.VersionConflict) reload()

        throw err
      }
    },
    [patchRow, reload],
  )

  const applyTerm = useCallback(
    (columnId: string, termName: string | null) => {
      const previous = rowsRef.current.find(row => row.columnId === columnId)?.businessTerm ?? null
      patchRow(columnId, { businessTerm: termName })
      return previous
    },
    [patchRow],
  )

  useEffect(() => {
    setIsLoading(true)
    setError(null)

    getColumns(query)
      .then(page => {
        setRows(page.items)
        setTotal(page.total)
      })
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : 'Failed to load columns.')
      })
      .finally(() => setIsLoading(false))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query.search, query.undocumentedOnly, query.tableName, query.sortBy, query.sortDir, query.limit, query.offset, tick])

  return { columns, total, isLoading, error, reload, editColumn, applyTerm }
}
