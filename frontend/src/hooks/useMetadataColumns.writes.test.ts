import { renderHook, act, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { BulkUpdateResponse, ColumnGridRow, ColumnUpdateRequest } from '../types/api'
import { useMetadataColumns, PAGE_SIZE } from './useMetadataColumns'
import { getColumns, bulkUpdateColumns } from '../services/metadataService'
import { setColumnBusinessTerm } from '../services/businessTermService'
import { ApiError, ApiErrorCode } from '../utils/api'

vi.mock('../services/metadataService', () => ({
  getColumns: vi.fn(),
  bulkUpdateColumns: vi.fn(),
}))

vi.mock('../services/businessTermService', () => ({
  setColumnBusinessTerm: vi.fn(),
  clearColumnBusinessTerm: vi.fn(),
}))

function deferred<T>() {
  let settle!: { resolve: (value: T) => void; reject: (err: unknown) => void }
  const promise = new Promise<T>((resolve, reject) => {
    settle = { resolve, reject }
  })
  return { promise, ...settle }
}

function makeRow(id: string, overrides: Partial<ColumnGridRow> = {}): ColumnGridRow {
  return {
    columnId: id,
    schemaName: 'sales',
    tableName: 'orders',
    columnName: `col_${id}`,
    dataType: 'text',
    exampleValue: '12.50',
    description: `Description ${id}`,
    businessTerm: null,
    owner: 'ana',
    version: 1,
    ...overrides,
  }
}

/** Every write, held open so the test decides when — and whether — the server answers. */
let writes: { requests: ColumnUpdateRequest[]; settle: ReturnType<typeof deferred<BulkUpdateResponse>> }[] = []

beforeEach(() => {
  vi.clearAllMocks()
  writes = []

  vi.mocked(getColumns).mockImplementation(async () => ({
    items: [makeRow('c1'), makeRow('c2')],
    total: 2,
    limit: PAGE_SIZE,
    offset: 0,
  }))

  vi.mocked(bulkUpdateColumns).mockImplementation((requests: ColumnUpdateRequest[]) => {
    const settle = deferred<BulkUpdateResponse>()
    writes.push({ requests, settle })
    return settle.promise
  })
})

async function renderLoaded() {
  const view = renderHook(() => useMetadataColumns({}))
  await waitFor(() => expect(view.result.current.columns).toHaveLength(2))
  return view
}

/** Swallows the rejection so a failed write under test is not also an unhandled one. */
function ignore(promise: Promise<void>): Promise<void> {
  return promise.catch(() => undefined)
}

describe('useMetadataColumns write queue', () => {
  it('sends an edit typed during an in-flight write with the version that write returned', async () => {
    const { result } = await renderLoaded()

    let first!: Promise<void>
    act(() => {
      first = ignore(result.current.editColumn('c1', { description: 'First' }))
    })

    await waitFor(() => expect(writes).toHaveLength(1))
    expect(writes[0].requests[0].version).toBe(1)

    // The user tabs to the next field and types before the first write has come back. Sending
    // this now would spend version 1 twice and the server would reject it as a conflict.
    let second!: Promise<void>
    act(() => {
      second = ignore(result.current.editColumn('c1', { owner: 'bob' }))
    })

    await act(async () => {
      await Promise.resolve()
    })
    expect(writes).toHaveLength(1)

    await act(async () => {
      writes[0].settle.resolve({ columns: [{ columnId: 'c1', version: 2 }], conflicts: [] })
      await first
    })

    await waitFor(() => expect(writes).toHaveLength(2))
    expect(writes[1].requests).toEqual([
      {
        columnId: 'c1',
        description: 'First',
        exampleValue: '12.50',
        owner: 'bob',
        version: 2,
      },
    ])

    await act(async () => {
      writes[1].settle.resolve({ columns: [{ columnId: 'c1', version: 3 }], conflicts: [] })
      await second
    })

    expect(result.current.columns[0].description).toBe('First')
    expect(result.current.columns[0].owner).toBe('bob')
    expect(result.current.columns[0].version).toBe(3)
  })

  it('batches edits made in one tick across rows into a single request', async () => {
    const { result } = await renderLoaded()

    act(() => {
      void ignore(result.current.editColumn('c1', { description: 'A' }))
      void ignore(result.current.editColumn('c2', { description: 'B' }))
    })

    await waitFor(() => expect(writes).toHaveLength(1))
    expect(writes[0].requests).toHaveLength(2)
    expect(writes[0].requests.map(r => r.columnId)).toEqual(['c1', 'c2'])
  })

  it('merges edits to different fields of one row into a single request row', async () => {
    const { result } = await renderLoaded()

    act(() => {
      void ignore(result.current.editColumn('c1', { description: 'A' }))
      void ignore(result.current.editColumn('c1', { owner: 'bob' }))
    })

    await waitFor(() => expect(writes).toHaveLength(1))
    expect(writes[0].requests).toHaveLength(1)
    expect(writes[0].requests[0]).toMatchObject({
      columnId: 'c1',
      description: 'A',
      owner: 'bob',
      version: 1,
    })
  })

  it('rolls every row in a failed batch back, and rejects each caller', async () => {
    const { result } = await renderLoaded()
    const failure = new ApiError(500, 'INTERNAL_ERROR', 'An unexpected error occurred.')

    let one!: Promise<unknown>
    let two!: Promise<unknown>
    act(() => {
      one = result.current.editColumn('c1', { description: 'A' }).catch(e => e)
      two = result.current.editColumn('c2', { description: 'B' }).catch(e => e)
    })

    await waitFor(() => expect(writes).toHaveLength(1))

    await act(async () => {
      writes[0].settle.reject(failure)
      await Promise.all([one, two])
    })

    expect(await one).toBe(failure)
    expect(await two).toBe(failure)
    expect(result.current.columns[0].description).toBe('Description c1')
    expect(result.current.columns[1].description).toBe('Description c2')
  })

  it('keeps the applied rows of a batch and puts back only the ones reported stale', async () => {
    const { result } = await renderLoaded()

    let applied!: Promise<unknown>
    let declined!: Promise<unknown>
    act(() => {
      applied = result.current.editColumn('c1', { description: 'Mine' }).then(
        () => 'resolved',
        e => e,
      )
      declined = result.current.editColumn('c2', { description: 'Mine too' }).then(
        () => 'resolved',
        e => e,
      )
    })

    await waitFor(() => expect(writes).toHaveLength(1))

    // What the rows look like once the server has taken one edit and rejected the other. The
    // conflict sends the grid back for that row's page, and both rows share one.
    vi.mocked(getColumns).mockImplementation(async () => ({
      items: [
        makeRow('c1', { description: 'Mine', version: 2 }),
        makeRow('c2', { description: 'Theirs', version: 9 }),
      ],
      total: 2,
      limit: PAGE_SIZE,
      offset: 0,
    }))

    // The server took one row and declined the other. One cell that moved under the user must
    // not undo the rest of what was pasted.
    await act(async () => {
      writes[0].settle.resolve({
        columns: [{ columnId: 'c1', version: 2 }],
        conflicts: [{ columnId: 'c2', currentVersion: 9 }],
      })
      await Promise.all([applied, declined])
    })

    expect(result.current.columns[0].description).toBe('Mine')
    expect(result.current.columns[0].version).toBe(2)

    // The declined row shows the winning value, not the edit that lost to it.
    await waitFor(() => expect(result.current.columns[1].description).toBe('Theirs'))
    expect(result.current.columns[1].version).toBe(9)

    // The declined row's caller still learns its edit did not land, as the same failure a
    // whole-batch conflict produces — so the grid reports it the same way.
    expect(await applied).toBe('resolved')
    const error = await declined
    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).code).toBe(ApiErrorCode.VersionConflict)
  })

  it('refetches the page of a row reported stale, so it stops carrying an unusable version', async () => {
    const { result } = await renderLoaded()
    const loadsBefore = vi.mocked(getColumns).mock.calls.length

    let declined!: Promise<unknown>
    act(() => {
      declined = result.current.editColumn('c2', { description: 'Mine' }).catch(e => e)
    })

    await waitFor(() => expect(writes).toHaveLength(1))

    await act(async () => {
      writes[0].settle.resolve({
        columns: [],
        conflicts: [{ columnId: 'c2', currentVersion: 9 }],
      })
      await declined
    })

    await waitFor(() =>
      expect(vi.mocked(getColumns).mock.calls.length).toBe(loadsBefore + 1),
    )
  })

  it('sends a pasted range as one request', async () => {
    const { result } = await renderLoaded()

    act(() => {
      void ignore(
        result.current.editColumns([
          { columnId: 'c1', edit: { description: 'A', owner: 'ana' } },
          { columnId: 'c2', edit: { description: 'B' } },
        ]),
      )
    })

    await waitFor(() => expect(writes).toHaveLength(1))
    expect(writes[0].requests).toHaveLength(2)
    expect(writes[0].requests[0]).toMatchObject({
      columnId: 'c1',
      description: 'A',
      owner: 'ana',
      version: 1,
    })
    expect(writes[0].requests[1]).toMatchObject({ columnId: 'c2', description: 'B' })

    // And every pasted cell is on screen before the server has said anything.
    expect(result.current.columns[0].description).toBe('A')
    expect(result.current.columns[0].owner).toBe('ana')
    expect(result.current.columns[1].description).toBe('B')
  })

  it('ignores pasted rows that are no longer in the window', async () => {
    const { result } = await renderLoaded()

    act(() => {
      void ignore(
        result.current.editColumns([
          { columnId: 'c1', edit: { description: 'A' } },
          { columnId: 'gone', edit: { description: 'B' } },
        ]),
      )
    })

    await waitFor(() => expect(writes).toHaveLength(1))
    expect(writes[0].requests.map(r => r.columnId)).toEqual(['c1'])
  })

  it('holds an edit back while a term write for that row is in flight', async () => {
    const { result } = await renderLoaded()

    // The term write moves the row's version, and only its response says what to.
    const mapping = deferred<{ columnId: string; version: number }>()
    vi.mocked(setColumnBusinessTerm).mockReturnValue(mapping.promise)

    let mapped!: Promise<void>
    act(() => {
      mapped = ignore(result.current.mapTerm('c1', 't1', 'Gross Revenue'))
    })

    expect(result.current.columns[0].businessTerm).toBe('Gross Revenue')

    // An edit to the same row, typed before the mapping came back.
    let edited!: Promise<void>
    act(() => {
      edited = ignore(result.current.editColumn('c1', { description: 'Mine' }))
    })

    await act(async () => {
      await Promise.resolve()
    })

    // Sending now would spend version 1, which the mapping is about to retire.
    expect(writes).toHaveLength(0)

    await act(async () => {
      mapping.resolve({ columnId: 'c1', version: 2 })
      await mapped
    })

    await waitFor(() => expect(writes).toHaveLength(1))
    expect(writes[0].requests[0]).toMatchObject({
      columnId: 'c1',
      description: 'Mine',
      version: 2,
    })

    await act(async () => {
      writes[0].settle.resolve({ columns: [{ columnId: 'c1', version: 3 }], conflicts: [] })
      await edited
    })

    expect(result.current.columns[0].version).toBe(3)
    expect(result.current.columns[0].businessTerm).toBe('Gross Revenue')
  })

  it('puts a failed term mapping back without disturbing the row otherwise', async () => {
    const { result } = await renderLoaded()

    const failure = new ApiError(500, 'INTERNAL_ERROR', 'An unexpected error occurred.')
    vi.mocked(setColumnBusinessTerm).mockRejectedValue(failure)

    let caught: unknown
    await act(async () => {
      caught = await result.current.mapTerm('c1', 't1', 'Gross Revenue').catch(e => e)
    })

    expect(caught).toBe(failure)
    expect(result.current.columns[0].businessTerm).toBeNull()
    expect(result.current.columns[0].version).toBe(1)
  })

  it('does not hold back edits to other rows while a term write is in flight', async () => {
    const { result } = await renderLoaded()

    const mapping = deferred<{ columnId: string; version: number }>()
    vi.mocked(setColumnBusinessTerm).mockReturnValue(mapping.promise)

    let mapped!: Promise<void>
    act(() => {
      mapped = ignore(result.current.mapTerm('c1', 't1', 'Gross Revenue'))
    })

    act(() => {
      void ignore(result.current.editColumn('c2', { description: 'Other row' }))
    })

    // Only c1's version is in play, so c2 has nothing to wait for.
    await waitFor(() => expect(writes).toHaveLength(1))
    expect(writes[0].requests.map(r => r.columnId)).toEqual(['c2'])

    await act(async () => {
      mapping.resolve({ columnId: 'c1', version: 2 })
      await mapped
    })
  })

  it('reconciles a whole batch in one pass over the window', async () => {
    const { result } = await renderLoaded()
    const before = result.current.columns

    act(() => {
      void ignore(result.current.editColumn('c1', { description: 'A' }))
      void ignore(result.current.editColumn('c2', { description: 'B' }))
    })

    await waitFor(() => expect(writes).toHaveLength(1))
    const optimistic = result.current.columns
    expect(optimistic).not.toBe(before)

    await act(async () => {
      writes[0].settle.resolve({
        columns: [
          { columnId: 'c1', version: 2 },
          { columnId: 'c2', version: 2 },
        ],
        conflicts: [],
      })
      await Promise.resolve()
    })

    // Both versions land together. Reconciling row by row rebuilt the whole array once per
    // row, which is what made a large paste crawl.
    expect(result.current.columns[0].version).toBe(2)
    expect(result.current.columns[1].version).toBe(2)
    expect(result.current.columns).not.toBe(optimistic)
  })

  it('keeps a queued edit on screen when the write it was typed over fails', async () => {
    const { result } = await renderLoaded()
    const failure = new ApiError(500, 'INTERNAL_ERROR', 'An unexpected error occurred.')

    let first!: Promise<void>
    act(() => {
      first = ignore(result.current.editColumn('c1', { description: 'First' }))
    })
    await waitFor(() => expect(writes).toHaveLength(1))

    let second!: Promise<void>
    act(() => {
      second = ignore(result.current.editColumn('c1', { description: 'Second' }))
    })

    await act(async () => {
      writes[0].settle.reject(failure)
      await first
    })

    // Rolling the cell back to the confirmed value here would discard what the user typed while
    // the first write was in flight — the queued edit still owns the cell.
    expect(result.current.columns[0].description).toBe('Second')

    await waitFor(() => expect(writes).toHaveLength(2))
    expect(writes[1].requests[0].description).toBe('Second')

    // When the queued write fails in turn, the cell falls back to what the server last
    // confirmed — not to the value the first, already-undone write was carrying.
    await act(async () => {
      writes[1].settle.reject(failure)
      await second
    })

    expect(result.current.columns[0].description).toBe('Description c1')
    expect(result.current.columns[0].version).toBe(1)
  })
})
