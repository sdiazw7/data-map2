import { renderHook, act, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ColumnGridRow } from '../types/api'
import { useMetadataColumns } from './useMetadataColumns'
import { getColumns, bulkUpdateColumns } from '../services/metadataService'
import { ApiError, ApiErrorCode } from '../utils/api'

vi.mock('../services/metadataService', () => ({
  getColumns: vi.fn(),
  bulkUpdateColumns: vi.fn(),
}))

const row: ColumnGridRow = {
  columnId: 'c1',
  schemaName: 'sales',
  tableName: 'orders',
  columnName: 'total_amount',
  dataType: 'numeric',
  exampleValue: '12.50',
  description: 'Original description',
  businessTerm: null,
  owner: 'ana',
  version: 1,
}

/** A promise the test resolves by hand, so the optimistic state can be observed mid-flight. */
function deferred<T>() {
  let settle!: { resolve: (value: T) => void; reject: (err: unknown) => void }
  const promise = new Promise<T>((resolve, reject) => {
    settle = { resolve, reject }
  })
  return { promise, ...settle }
}

async function renderLoaded() {
  vi.mocked(getColumns).mockResolvedValue({ items: [row], total: 1, limit: 200, offset: 0 })

  const view = renderHook(() => useMetadataColumns({}))
  await waitFor(() => expect(view.result.current.columns).toHaveLength(1))
  return view
}

describe('useMetadataColumns.editColumn', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('shows the edit before the server confirms it, then takes the new version', async () => {
    const { result } = await renderLoaded()
    const write = deferred<{ columns: { columnId: string; version: number }[] }>()
    vi.mocked(bulkUpdateColumns).mockReturnValue(write.promise)

    let edit!: Promise<void>
    act(() => {
      edit = result.current.editColumn('c1', { description: 'Updated' })
    })

    // In flight: the value is already on screen, the version is not yet spent.
    expect(result.current.columns[0].description).toBe('Updated')
    expect(result.current.columns[0].version).toBe(1)

    // The request carries the version the server last confirmed.
    await waitFor(() =>
      expect(vi.mocked(bulkUpdateColumns)).toHaveBeenCalledWith([
        { columnId: 'c1', description: 'Updated', exampleValue: '12.50', owner: 'ana', version: 1 },
      ]),
    )

    await act(async () => {
      write.resolve({ columns: [{ columnId: 'c1', version: 2 }] })
      await edit
    })

    expect(result.current.columns[0].description).toBe('Updated')
    expect(result.current.columns[0].version).toBe(2)
  })

  it('puts the row back and rethrows when the write fails', async () => {
    const { result } = await renderLoaded()
    const failure = new ApiError(500, 'INTERNAL_ERROR', 'An unexpected error occurred.')
    vi.mocked(bulkUpdateColumns).mockRejectedValue(failure)

    let caught: unknown
    await act(async () => {
      caught = await result.current.editColumn('c1', { description: 'Updated' }).catch(e => e)
    })

    expect(caught).toBe(failure)
    expect(result.current.columns[0].description).toBe('Original description')
    expect(result.current.columns[0].version).toBe(1)
  })

  it('reloads on a version conflict, so the row stops carrying a version that can never win', async () => {
    const { result } = await renderLoaded()
    expect(vi.mocked(getColumns)).toHaveBeenCalledTimes(1)

    vi.mocked(bulkUpdateColumns).mockRejectedValue(
      new ApiError(409, ApiErrorCode.VersionConflict, 'The column changed since you loaded it.'),
    )

    await act(async () => {
      await result.current.editColumn('c1', { description: 'Updated' }).catch(() => undefined)
    })

    expect(result.current.columns[0].description).toBe('Original description')
    await waitFor(() => expect(vi.mocked(getColumns)).toHaveBeenCalledTimes(2))
  })

  it('rolls back only the fields it changed, leaving a term mapped mid-flight in place', async () => {
    const { result } = await renderLoaded()
    const write = deferred<{ columns: { columnId: string; version: number }[] }>()
    vi.mocked(bulkUpdateColumns).mockReturnValue(write.promise)

    let edit!: Promise<void>
    act(() => {
      edit = result.current.editColumn('c1', { description: 'Updated' })
    })

    act(() => {
      result.current.applyTerm('c1', 'Gross Revenue')
    })

    await act(async () => {
      write.reject(new ApiError(500, 'INTERNAL_ERROR', 'An unexpected error occurred.'))
      await edit.catch(() => undefined)
    })

    expect(result.current.columns[0].description).toBe('Original description')
    expect(result.current.columns[0].businessTerm).toBe('Gross Revenue')
  })
})

describe('useMetadataColumns.applyTerm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('returns the term it replaced so a failed mapping can be undone', async () => {
    const { result } = await renderLoaded()

    let previous: string | null = 'unset'
    act(() => {
      previous = result.current.applyTerm('c1', 'Gross Revenue')
    })
    expect(previous).toBeNull()
    expect(result.current.columns[0].businessTerm).toBe('Gross Revenue')

    act(() => {
      previous = result.current.applyTerm('c1', null)
    })
    expect(previous).toBe('Gross Revenue')
    expect(result.current.columns[0].businessTerm).toBeNull()
  })
})
