import { renderHook, act, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ColumnGridRow, PagedResult } from '../types/api'
import type { ColumnsQuery } from '../services/metadataService'
import { useMetadataColumns, PAGE_SIZE } from './useMetadataColumns'
import { getColumns, bulkUpdateColumns } from '../services/metadataService'
import { ApiError, ApiErrorCode } from '../utils/api'

vi.mock('../services/metadataService', () => ({
  getColumns: vi.fn(),
  bulkUpdateColumns: vi.fn(),
}))

type Page = PagedResult<ColumnGridRow>

function deferred<T>() {
  let settle!: { resolve: (value: T) => void; reject: (err: unknown) => void }
  const promise = new Promise<T>((resolve, reject) => {
    settle = { resolve, reject }
  })
  return { promise, ...settle }
}

function makeRow(index: number, overrides: Partial<ColumnGridRow> = {}): ColumnGridRow {
  return {
    columnId: `c${index}`,
    schemaName: 'sales',
    tableName: 'orders',
    columnName: `col_${index}`,
    dataType: 'text',
    exampleValue: null,
    description: `Description ${index}`,
    businessTerm: null,
    owner: null,
    version: 1,
    ...overrides,
  }
}

function makePage(offset: number, count: number, total: number): Page {
  return {
    items: Array.from({ length: count }, (_, i) => makeRow(offset + i)),
    total,
    limit: PAGE_SIZE,
    offset,
  }
}

/** Every getColumns call, held open so the test decides when — and whether — it resolves. */
let calls: { query: ColumnsQuery; settle: ReturnType<typeof deferred<Page>> }[] = []

beforeEach(() => {
  vi.clearAllMocks()
  calls = []
  vi.mocked(getColumns).mockImplementation((query: ColumnsQuery) => {
    const settle = deferred<Page>()
    calls.push({ query, settle })
    return settle.promise
  })
})

/** Resolves the call at `index` and lets React flush the state it produces. */
async function resolveCall(index: number, page: Page) {
  await act(async () => {
    calls[index].settle.resolve(page)
    await calls[index].settle.promise
  })
}

function renderPaged(query: ColumnsQuery = {}) {
  return renderHook(({ q }: { q: ColumnsQuery }) => useMetadataColumns(q), {
    initialProps: { q: query },
  })
}

describe('useMetadataColumns paging', () => {
  it('asks for a bounded first page instead of relying on a server default', async () => {
    renderPaged()

    await waitFor(() => expect(calls).toHaveLength(1))
    expect(calls[0].query.limit).toBe(PAGE_SIZE)
    expect(calls[0].query.offset).toBe(0)
  })

  it('appends the next page at the offset after what is loaded', async () => {
    const { result } = renderPaged()

    await waitFor(() => expect(calls).toHaveLength(1))
    await resolveCall(0, makePage(0, PAGE_SIZE, 450))

    expect(result.current.columns).toHaveLength(PAGE_SIZE)
    expect(result.current.total).toBe(450)
    expect(result.current.hasMore).toBe(true)

    act(() => result.current.loadMore())

    await waitFor(() => expect(calls).toHaveLength(2))
    expect(calls[1].query.offset).toBe(PAGE_SIZE)
    expect(calls[1].query.limit).toBe(PAGE_SIZE)

    await resolveCall(1, makePage(PAGE_SIZE, PAGE_SIZE, 450))

    expect(result.current.columns).toHaveLength(PAGE_SIZE * 2)
    expect(result.current.columns[PAGE_SIZE].columnId).toBe(`c${PAGE_SIZE}`)
    expect(result.current.hasMore).toBe(true)
  })

  it('stops asking once the whole result set is loaded', async () => {
    const { result } = renderPaged()

    await waitFor(() => expect(calls).toHaveLength(1))
    await resolveCall(0, makePage(0, 3, 3))

    expect(result.current.hasMore).toBe(false)

    act(() => result.current.loadMore())

    expect(calls).toHaveLength(1)
  })

  it('collapses repeated loadMore calls into a single request', async () => {
    const { result } = renderPaged()

    await waitFor(() => expect(calls).toHaveLength(1))
    await resolveCall(0, makePage(0, PAGE_SIZE, 450))

    // The grid calls this from a scroll effect, so it fires far more often than it needs to.
    act(() => {
      result.current.loadMore()
      result.current.loadMore()
      result.current.loadMore()
    })

    expect(calls).toHaveLength(2)
  })

  it('discards a page that arrives after the filters changed', async () => {
    const { result, rerender } = renderPaged({ search: 'orders' })

    await waitFor(() => expect(calls).toHaveLength(1))
    await resolveCall(0, makePage(0, PAGE_SIZE, 450))

    // A second page is in flight for the old filters...
    act(() => result.current.loadMore())
    await waitFor(() => expect(calls).toHaveLength(2))

    // ...when the user searches for something else.
    rerender({ q: { search: 'customers' } })
    await waitFor(() => expect(calls).toHaveLength(3))
    expect(calls[2].query.offset).toBe(0)
    expect(calls[2].query.search).toBe('customers')

    await resolveCall(2, { items: [makeRow(999)], total: 1, limit: PAGE_SIZE, offset: 0 })
    expect(result.current.columns).toHaveLength(1)

    // The stale page lands last. Appending it would splice 200 rows of the previous search
    // onto the end of this one.
    await resolveCall(1, makePage(PAGE_SIZE, PAGE_SIZE, 450))

    expect(result.current.columns).toHaveLength(1)
    expect(result.current.columns[0].columnId).toBe('c999')
    expect(result.current.total).toBe(1)
  })

  it('refreshes only the conflicted row page, keeping the rest of the window', async () => {
    const { result } = renderPaged()

    await waitFor(() => expect(calls).toHaveLength(1))
    await resolveCall(0, makePage(0, PAGE_SIZE, 450))

    act(() => result.current.loadMore())
    await waitFor(() => expect(calls).toHaveLength(2))
    await resolveCall(1, makePage(PAGE_SIZE, PAGE_SIZE, 450))
    expect(result.current.columns).toHaveLength(PAGE_SIZE * 2)

    vi.mocked(bulkUpdateColumns).mockRejectedValue(
      new ApiError(409, ApiErrorCode.VersionConflict, 'The column changed since you loaded it.'),
    )

    // A row on the second page, to prove the refresh targets that page and not the first.
    const conflicted = `c${PAGE_SIZE + 5}`
    await act(async () => {
      await result.current.editColumn(conflicted, { description: 'Mine' }).catch(() => undefined)
    })

    await waitFor(() => expect(calls).toHaveLength(3))
    expect(calls[2].query.offset).toBe(PAGE_SIZE)

    const winner = makeRow(PAGE_SIZE + 5, { description: 'Theirs', version: 7 })
    const refreshed = makePage(PAGE_SIZE, PAGE_SIZE, 450)
    refreshed.items[5] = winner
    await resolveCall(2, refreshed)

    // The window is intact and the row now carries the winning value and a usable version.
    expect(result.current.columns).toHaveLength(PAGE_SIZE * 2)
    expect(result.current.columns[PAGE_SIZE + 5].description).toBe('Theirs')
    expect(result.current.columns[PAGE_SIZE + 5].version).toBe(7)
    expect(result.current.columns[0].columnId).toBe('c0')
  })
})
