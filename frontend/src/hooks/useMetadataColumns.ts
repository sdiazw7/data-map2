import { useState, useEffect, useCallback, useRef } from 'react'
import type { ColumnGridRow, ColumnUpdateRequest } from '../types/api'
import type { ColumnsQuery } from '../services/metadataService'
import { getColumns, bulkUpdateColumns } from '../services/metadataService'
import { ApiError, ApiErrorCode } from '../utils/api'

/** The editable fields of a row. A cell sends only the one it changed. */
export type ColumnEdit = Partial<Pick<ColumnGridRow, 'description' | 'exampleValue' | 'owner'>>

/**
 * Rows per request. A workspace holds 100k+ columns, so the grid holds a window over them and
 * extends it as the user scrolls; the server caps a page at 1,000 either way.
 */
export const PAGE_SIZE = 200

type UseMetadataColumnsResult = {
  /** The rows loaded so far, from the first through the furthest the user has scrolled. */
  columns: ColumnGridRow[]
  /** Rows matching the current filters across all pages, not just the ones loaded. */
  total: number
  /** True while the first page of the current filters is loading. */
  isLoading: boolean
  /** True while a further page is being appended, with rows already on screen. */
  isLoadingMore: boolean
  /** Whether any rows matching the filters have yet to be loaded. */
  hasMore: boolean
  error: string | null
  /** Appends the next page. Safe to call on every scroll frame; redundant calls are dropped. */
  loadMore: () => void
  /** Discards the window and reloads from the first page. */
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
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  // An edit has to read the row it is editing — for its current version, and for the values to
  // restore if the write fails — and React state is a render behind by the time a handler runs.
  // Every write to `columns` goes through setRows so the two never diverge.
  const rowsRef = useRef<ColumnGridRow[]>([])
  const totalRef = useRef(0)

  // Paging turns a stale response into corruption rather than a flicker: page 3 of the filters
  // the user just left would append onto page 1 of the ones they moved to. Loading the first
  // page opens a generation, and a response from a spent one is dropped.
  const generationRef = useRef(0)
  const isLoadingMoreRef = useRef(false)

  // loadMore is called from a scroll handler and has to stay referentially stable, so it reads
  // the current filters from here rather than closing over them.
  const queryRef = useRef(query)
  queryRef.current = query

  const setRows = useCallback(
    (next: ColumnGridRow[] | ((prev: ColumnGridRow[]) => ColumnGridRow[])) => {
      const value = typeof next === 'function' ? next(rowsRef.current) : next
      rowsRef.current = value
      setColumns(value)
    },
    [],
  )

  const setTotalCount = useCallback((value: number) => {
    totalRef.current = value
    setTotal(value)
  }, [])

  const patchRow = useCallback(
    (columnId: string, patch: Partial<ColumnGridRow>) => {
      setRows(rows => rows.map(row => (row.columnId === columnId ? { ...row, ...patch } : row)))
    },
    [setRows],
  )

  const reload = useCallback(() => setTick(t => t + 1), [])

  const loadMore = useCallback(() => {
    if (isLoadingMoreRef.current) return

    const offset = rowsRef.current.length
    if (offset === 0 || offset >= totalRef.current) return

    const generation = generationRef.current
    isLoadingMoreRef.current = true
    setIsLoadingMore(true)

    getColumns({ ...queryRef.current, limit: PAGE_SIZE, offset })
      .then(page => {
        if (generation !== generationRef.current) return

        // Appended by position rather than merged by id: the server returned this page under
        // the same filters and sort, so it continues the window the grid already holds.
        setRows(rows => [...rows, ...page.items])
        setTotalCount(page.total)
      })
      .catch((err: unknown) => {
        if (generation !== generationRef.current) return
        setError(err instanceof Error ? err.message : 'Failed to load more columns.')
      })
      .finally(() => {
        if (generation !== generationRef.current) return
        isLoadingMoreRef.current = false
        setIsLoadingMore(false)
      })
  }, [setRows, setTotalCount])

  /**
   * Reloads the page a row sits on. Used after a version conflict: the whole window could be
   * discarded instead, but that would drop a user who is 5,000 rows deep back at the top.
   */
  const refreshRowPage = useCallback(
    async (columnId: string) => {
      const index = rowsRef.current.findIndex(row => row.columnId === columnId)
      if (index < 0) return

      const offset = Math.floor(index / PAGE_SIZE) * PAGE_SIZE
      const generation = generationRef.current

      try {
        const page = await getColumns({ ...queryRef.current, limit: PAGE_SIZE, offset })
        if (generation !== generationRef.current) return

        setRows(rows => {
          const next = [...rows]
          page.items.forEach((row, i) => {
            if (offset + i < next.length) next[offset + i] = row
          })
          return next
        })
        setTotalCount(page.total)
      } catch {
        // Best effort. The edit has already been rolled back and reported, and leaving the row
        // stale does not make that worse.
      }
    },
    [setRows, setTotalCount],
  )

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
        // is already out of date — only a refetch gets the winning values and a usable version.
        if (err instanceof ApiError && err.code === ApiErrorCode.VersionConflict) {
          void refreshRowPage(columnId)
        }

        throw err
      }
    },
    [patchRow, refreshRowPage],
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
    // Opening a generation retires any page still in flight for the previous filters.
    const generation = ++generationRef.current
    const controller = new AbortController()

    isLoadingMoreRef.current = false
    setIsLoadingMore(false)
    setIsLoading(true)
    setError(null)
    setRows([])
    setTotalCount(0)

    getColumns({ ...query, limit: PAGE_SIZE, offset: 0 }, controller.signal)
      .then(page => {
        if (generation !== generationRef.current) return
        setRows(page.items)
        setTotalCount(page.total)
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted || generation !== generationRef.current) return
        setError(err instanceof Error ? err.message : 'Failed to load columns.')
      })
      .finally(() => {
        if (generation !== generationRef.current) return
        setIsLoading(false)
      })

    return () => controller.abort()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query.search, query.undocumentedOnly, query.tableName, query.sortBy, query.sortDir, tick])

  return {
    columns,
    total,
    isLoading,
    isLoadingMore,
    hasMore: columns.length < total,
    error,
    loadMore,
    reload,
    editColumn,
    applyTerm,
  }
}
