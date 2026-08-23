import { useState, useEffect, useCallback, useRef } from 'react'
import type { ColumnGridRow } from '../types/api'
import type { ColumnsQuery } from '../services/metadataService'
import { getColumns } from '../services/metadataService'
import type { ColumnEdit, ColumnEdits } from '../utils/columnFields'
import { useWriteQueue } from './useWriteQueue'

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
  /**
   * The same for a set of rows at once — what a pasted range uses. Every edit lands in one
   * request; the promise rejects with the first failure once all of them have settled.
   */
  editColumns: (edits: ColumnEdits) => Promise<void>
  /**
   * Sets a row's business term locally, returning the term it replaced so a failed write can
   * undo it. Called again with the version the server returned once the write has landed.
   */
  applyTerm: (columnId: string, termName: string | null, version?: number) => string | null
  /**
   * Maps a business term onto a row, or clears it when termId is empty. Shows the change
   * immediately and takes it back if the write fails, rejecting with the {@link ApiError}.
   * Ordered against the edit queue: both this and a grid edit move the row's version, so one
   * waits for the other rather than spending a version the server has already retired.
   */
  mapTerm: (columnId: string, termId: string, termName: string | null) => Promise<void>
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

  /**
   * Applies one patch per row in a single pass. Reconciling a batch used to call patchRow once
   * per row, and every one of those mapped the whole window: a 500-row paste over a window of
   * 100k rows walked 50M rows and produced 500 new arrays, for what is one change to the grid.
   */
  const patchRows = useCallback(
    (patches: Map<string, Partial<ColumnGridRow>>) => {
      if (patches.size === 0) return

      setRows(rows =>
        rows.map(row => {
          const patch = patches.get(row.columnId)
          return patch ? { ...row, ...patch } : row
        }),
      )
    },
    [setRows],
  )

  const patchRow = useCallback(
    (columnId: string, patch: Partial<ColumnGridRow>) => patchRows(new Map([[columnId, patch]])),
    [patchRows],
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
   * Reloads the pages the given rows sit on. Used after a version conflict: the whole window
   * could be discarded instead, but that would drop a user who is 5,000 rows deep back at the
   * top. A batch can span pages, and several of its rows usually share one, so the pages are
   * collapsed to a set before any of them is fetched.
   */
  const refreshPagesFor = useCallback(
    async (columnIds: string[]) => {
      const offsets = new Set<number>()
      for (const columnId of columnIds) {
        const index = rowsRef.current.findIndex(row => row.columnId === columnId)
        if (index < 0) continue
        offsets.add(Math.floor(index / PAGE_SIZE) * PAGE_SIZE)
      }

      const generation = generationRef.current

      for (const offset of offsets) {
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
          // Best effort. The edit has already been rolled back and reported, and leaving the
          // row stale does not make that worse.
        }
      }
    },
    [setRows, setTotalCount],
  )

  const applyTerm = useCallback(
    (columnId: string, termName: string | null, version?: number) => {
      const previous = rowsRef.current.find(row => row.columnId === columnId)?.businessTerm ?? null

      const patch: Partial<ColumnGridRow> = { businessTerm: termName }

      // Mapping a term moves the row's version server-side. Taking the new one here is what
      // keeps the next grid edit to this row from spending a version already retired.
      if (version !== undefined) patch.version = version

      patchRow(columnId, patch)
      return previous
    },
    [patchRow],
  )

  /**
   * The queue reads the window when it sends, not when the edit was made, so it takes a getter
   * rather than a snapshot: a batch queued three keystrokes ago must still see current rows.
   */
  const getRows = useCallback(() => rowsRef.current, [])

  const { editColumn, editColumns, mapTerm } = useWriteQueue({
    getRows,
    patchRows,
    applyTerm,
    onVersionConflict: refreshPagesFor,
  })

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
    editColumns,
    applyTerm,
    mapTerm,
  }
}
