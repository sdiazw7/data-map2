import { useState, useEffect, useCallback, useRef } from 'react'
import type { ColumnGridRow, ColumnUpdateRequest } from '../types/api'
import type { ColumnsQuery } from '../services/metadataService'
import { getColumns, bulkUpdateColumns } from '../services/metadataService'
import { setColumnBusinessTerm, clearColumnBusinessTerm } from '../services/businessTermService'
import { ApiError, ApiErrorCode } from '../utils/api'
import type { ColumnEdit, ColumnEdits } from '../utils/columnFields'
import { touchedFields } from '../utils/columnFields'

/**
 * What a caller sees when the server declined its row as stale. The batch itself succeeded, so
 * there is no thrown error to hand on — but the caller of this one row still has to learn that
 * its edit did not land, and to learn it as the same failure a whole-batch conflict produces.
 */
function versionConflictError(): ApiError {
  return new ApiError(
    409,
    ApiErrorCode.VersionConflict,
    'The column was modified by someone else. Please refresh and try again.',
  )
}

/**
 * Rows per request. A workspace holds 100k+ columns, so the grid holds a window over them and
 * extends it as the user scrolls; the server caps a page at 1,000 either way.
 */
export const PAGE_SIZE = 200

/**
 * One row's queued write. Edits merge into it until it is sent, so a run of keystrokes across
 * a row costs one request rather than one per field.
 */
type PendingWrite = {
  /**
   * The values to restore if the write fails, per field this cycle touched. Recorded the first
   * time a field is touched, so it holds what the server last confirmed rather than a value an
   * earlier edit in the same cycle put on screen.
   */
  before: ColumnEdit
  /** The row as last seen, used if it has left the window by the time the write is sent. */
  snapshot: ColumnGridRow
  /** One per editColumn call merged into this write; all settle together. */
  resolvers: { resolve: () => void; reject: (err: unknown) => void }[]
}

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

  // The write queue. Edits land in `pending` and leave in batches; `inFlight` holds the batch
  // being written, whose values the server has not confirmed and so cannot be rolled back onto.
  const pendingRef = useRef(new Map<string, PendingWrite>())
  const inFlightRef = useRef(new Map<string, PendingWrite>())
  const isFlushingRef = useRef(false)
  const flushScheduledRef = useRef(false)
  const flushRef = useRef<() => void>(() => {})

  // Rows with a business-term write in flight. A term mapping moves the row's version too, so
  // a queued edit has to wait for it rather than spend a version the server is about to retire.
  const termWritesRef = useRef(new Set<string>())

  // Settles when the batch being written finishes, so a term write can wait its turn behind it.
  const inFlightDoneRef = useRef<Promise<void> | null>(null)

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

  /**
   * The patches that undo a batch's optimistic changes, field by field. Only the fields the
   * batch touched are undone, so a term mapping that landed on one of the rows in the meantime
   * survives. Returned rather than applied so a partly-applied batch can undo the rows the
   * server declined in the same pass that confirms the ones it took.
   */
  const rollBackPatches = useCallback((entries: [string, PendingWrite][]) => {
    const patches = new Map<string, ColumnEdit>()

    for (const [columnId, write] of entries) {
      const queued = pendingRef.current.get(columnId)
      const revert: ColumnEdit = {}

      for (const field of touchedFields(write.before)) {
        // A newer edit to this field is already queued. It owns what the cell shows, and it
        // carries the same confirmed baseline, so undoing it here would throw away the
        // keystrokes the user typed while this write was in flight.
        if (queued && field in queued.before) continue
        revert[field] = write.before[field]
      }

      patches.set(columnId, revert)
    }

    return patches
  }, [])

  const scheduleFlush = useCallback(() => {
    if (flushScheduledRef.current || isFlushingRef.current) return
    flushScheduledRef.current = true

    // A microtask rather than a timer: every edit made in this tick — a run of keystrokes, a
    // pasted range — batches into one request, and a lone edit still leaves immediately.
    queueMicrotask(() => flushRef.current())
  }, [])

  const flush = useCallback(async () => {
    flushScheduledRef.current = false
    if (isFlushingRef.current || pendingRef.current.size === 0) return

    // A row with a term write in the air is left on the queue: that write moves its version,
    // and the value it returns is the one the next edit has to carry.
    const batch = [...pendingRef.current.entries()].filter(
      ([columnId]) => !termWritesRef.current.has(columnId),
    )
    if (batch.length === 0) return

    for (const [columnId] of batch) pendingRef.current.delete(columnId)
    inFlightRef.current = new Map(batch)
    isFlushingRef.current = true

    let releaseInFlight!: () => void
    inFlightDoneRef.current = new Promise<void>(resolve => {
      releaseInFlight = resolve
    })

    // The version is read here and not when the edit was made. An edit typed while an earlier
    // write for the same row was still in flight would otherwise carry the version that write
    // was about to spend, and the server would reject the user's own keystrokes as a conflict.
    const requests: ColumnUpdateRequest[] = batch.map(([columnId, write]) => {
      const row = rowsRef.current.find(r => r.columnId === columnId) ?? write.snapshot
      return {
        columnId,
        description: row.description,
        exampleValue: row.exampleValue,
        owner: row.owner,
        version: row.version,
      }
    })

    try {
      const result = await bulkUpdateColumns(requests)

      // The response carries each applied row's new version, so those rows are reconciled in
      // place. Without it they would keep the versions they just spent and every later edit
      // would conflict.
      const versions = new Map(result.columns.map(c => [c.columnId, c.version]))
      const declined = new Set(result.conflicts.map(c => c.columnId))

      // Confirmations and undos go on in one pass. They never touch the same row — the server
      // returns a row as applied or as declined, never both.
      const patches = new Map<string, Partial<ColumnGridRow>>()
      for (const [columnId] of batch) {
        const version = versions.get(columnId)
        if (version !== undefined) patches.set(columnId, { version })
      }

      // The rows the server declined as stale. The rest of the batch was applied, so only
      // these are put back — one cell that moved under the user does not undo the others.
      const conflicted = batch.filter(([columnId]) => declined.has(columnId))
      for (const [columnId, revert] of rollBackPatches(conflicted)) {
        patches.set(columnId, revert)
      }

      patchRows(patches)

      // Rolling back restores what we last saw, which is already out of date — only a refetch
      // gets the winning values and a version the next edit can spend.
      if (conflicted.length > 0) {
        void refreshPagesFor(conflicted.map(([columnId]) => columnId))
      }

      for (const [columnId, write] of batch) {
        if (declined.has(columnId)) {
          write.resolvers.forEach(r => r.reject(versionConflictError()))
        } else {
          write.resolvers.forEach(r => r.resolve())
        }
      }
    } catch (err: unknown) {
      patchRows(rollBackPatches(batch))

      // The database rejected the write outright, which it cannot attribute to a single row,
      // so the whole batch failed and every row in it needs its winning values back.
      if (err instanceof ApiError && err.code === ApiErrorCode.VersionConflict) {
        void refreshPagesFor(batch.map(([columnId]) => columnId))
      }

      for (const [, write] of batch) write.resolvers.forEach(r => r.reject(err))
    } finally {
      inFlightRef.current = new Map()
      isFlushingRef.current = false
      inFlightDoneRef.current = null
      releaseInFlight()

      // Edits made while this batch was in flight are waiting on the version it just returned.
      if (pendingRef.current.size > 0) scheduleFlush()
    }
  }, [patchRows, rollBackPatches, refreshPagesFor, scheduleFlush])

  flushRef.current = () => void flush()

  /**
   * Puts one row's edit on the queue and hands back the promise for it. Does not patch the row
   * or schedule the flush — the caller does both once for the whole set, so a pasted range
   * costs one pass over the window rather than one per cell.
   */
  const queueEdit = useCallback((row: ColumnGridRow, edit: ColumnEdit): Promise<void> => {
    const columnId = row.columnId
    const queued = pendingRef.current.get(columnId)
    const write: PendingWrite = queued ?? { before: {}, snapshot: row, resolvers: [] }

    for (const field of touchedFields(edit)) {
      if (field in write.before) continue

      // What to restore is the last value the server confirmed. While a write for this row is
      // in flight the row on screen holds values it has not confirmed yet, so the baseline
      // comes from that write instead — otherwise a second failure would restore a value the
      // first one had already rolled back.
      const inFlight = inFlightRef.current.get(columnId)
      write.before[field] =
        inFlight && field in inFlight.before ? inFlight.before[field] : row[field]
    }

    write.snapshot = { ...write.snapshot, ...edit }

    if (!queued) pendingRef.current.set(columnId, write)

    return new Promise<void>((resolve, reject) => {
      write.resolvers.push({ resolve, reject })
    })
  }, [])

  const editColumns = useCallback(
    (edits: ColumnEdits): Promise<void> => {
      if (edits.length === 0) return Promise.resolve()

      // One index of the window for the whole set. Looking each row up by scanning would put a
      // pass over 100k rows on every cell of a pasted range.
      const rowsById = new Map(rowsRef.current.map(row => [row.columnId, row]))

      const patches = new Map<string, Partial<ColumnGridRow>>()
      const settled: Promise<void>[] = []

      for (const { columnId, edit } of edits) {
        const row = rowsById.get(columnId)
        if (!row) continue

        // Two cells of the same row arrive as two edits; they belong to one patch and one write.
        patches.set(columnId, { ...patches.get(columnId), ...edit })
        settled.push(queueEdit(row, edit))
      }

      if (settled.length === 0) return Promise.resolve()

      // Optimistic: the cells show their new values now. The queue either confirms them or
      // takes them back.
      patchRows(patches)
      scheduleFlush()

      return Promise.all(settled).then(() => undefined)
    },
    [patchRows, queueEdit, scheduleFlush],
  )

  const editColumn = useCallback(
    (columnId: string, edit: ColumnEdit) => editColumns([{ columnId, edit }]),
    [editColumns],
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

  const mapTerm = useCallback(
    async (columnId: string, termId: string, termName: string | null): Promise<void> => {
      // Applied first so the cell moves with the click; applyTerm hands back what it replaced.
      const previous = applyTerm(columnId, termName)

      // Held from here rather than after the await, so a flush scheduled in between sees it.
      termWritesRef.current.add(columnId)

      try {
        // A grid write for this row may already be in the air. Both writes move the version, so
        // they have to be ordered — otherwise the second reads a version the first has spent.
        if (inFlightRef.current.has(columnId) && inFlightDoneRef.current) {
          await inFlightDoneRef.current
        }

        // The empty option in the term cell means "no term", which is a delete, not a mapping.
        const result = termId
          ? await setColumnBusinessTerm(columnId, termId)
          : await clearColumnBusinessTerm(columnId)

        applyTerm(columnId, termName, result.version)
      } catch (err: unknown) {
        applyTerm(columnId, previous)
        throw err
      } finally {
        termWritesRef.current.delete(columnId)

        // Edits held back while this was in flight can go now, carrying the version it returned.
        if (pendingRef.current.size > 0) scheduleFlush()
      }
    },
    [applyTerm, scheduleFlush],
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
    editColumns,
    applyTerm,
    mapTerm,
  }
}
