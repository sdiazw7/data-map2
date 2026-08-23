import { useCallback, useRef } from 'react'
import type { ColumnGridRow, ColumnUpdateRequest } from '../types/api'
import { bulkUpdateColumns } from '../services/metadataService'
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

type UseWriteQueueOptions = {
  /**
   * The rows as they stand now. Read when a write is actually sent rather than when it was
   * queued, which is what keeps a row's version current.
   */
  getRows: () => ColumnGridRow[]
  /** Applies one patch per row in a single pass over the window. */
  patchRows: (patches: Map<string, Partial<ColumnGridRow>>) => void
  /** Sets a row's term locally, returning what it replaced so a failed mapping can undo it. */
  applyTerm: (columnId: string, termName: string | null, version?: number) => string | null
  /**
   * Called with the rows a conflict touched. Their values on screen are out of date by
   * definition, so only a refetch gets the winning ones and a version the next edit can spend.
   */
  onVersionConflict: (columnIds: string[]) => void
}

type UseWriteQueueResult = {
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
   * Maps a business term onto a row, or clears it when termId is empty. Ordered against the
   * edit queue: both this and a grid edit move the row's version, so one waits for the other
   * rather than spending a version the server has already retired.
   */
  mapTerm: (columnId: string, termId: string, termName: string | null) => Promise<void>
}

/**
 * Serialises the grid's writes. Two rules hold the whole thing up:
 *
 * - one write per row is in flight at a time, so two edits never race for the same version;
 * - a row's version is read when its write is sent, never when the edit was made.
 *
 * Everything else — batching a tick's worth of edits into one request, merging a row's fields,
 * undoing what the server declined — falls out of those.
 */
export function useWriteQueue({
  getRows,
  patchRows,
  applyTerm,
  onVersionConflict,
}: UseWriteQueueOptions): UseWriteQueueResult {
  // Edits land in `pending` and leave in batches; `inFlight` holds the batch being written,
  // whose values the server has not confirmed and so cannot be rolled back onto.
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
    const rows = getRows()
    const requests: ColumnUpdateRequest[] = batch.map(([columnId, write]) => {
      const row = rows.find(r => r.columnId === columnId) ?? write.snapshot
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

      if (conflicted.length > 0) {
        onVersionConflict(conflicted.map(([columnId]) => columnId))
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
        onVersionConflict(batch.map(([columnId]) => columnId))
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
  }, [getRows, patchRows, rollBackPatches, onVersionConflict, scheduleFlush])

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
      const rowsById = new Map(getRows().map(row => [row.columnId, row]))

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
    [getRows, patchRows, queueEdit, scheduleFlush],
  )

  const editColumn = useCallback(
    (columnId: string, edit: ColumnEdit) => editColumns([{ columnId, edit }]),
    [editColumns],
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

  return { editColumn, editColumns, mapTerm }
}
