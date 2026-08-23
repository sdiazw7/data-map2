import { renderHook, waitFor, act } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { MetadataChange, PagedResult } from '../types/api'
import { useColumnHistory } from './useColumnHistory'
import { getColumnHistory } from '../services/metadataService'

vi.mock('../services/metadataService', () => ({
  getColumnHistory: vi.fn(),
}))

function deferred<T>() {
  let settle!: { resolve: (value: T) => void; reject: (err: unknown) => void }
  const promise = new Promise<T>((resolve, reject) => {
    settle = { resolve, reject }
  })
  return { promise, ...settle }
}

function makeChange(id: string, field: string): MetadataChange {
  return {
    id,
    field,
    oldValue: 'old',
    newValue: 'new',
    editedByEmail: 'ana@example.com',
    editedAt: '2026-08-23T10:00:00Z',
  }
}

function page(changes: MetadataChange[], total = changes.length): PagedResult<MetadataChange> {
  return { items: changes, total, limit: 50, offset: 0 }
}

describe('useColumnHistory', () => {
  beforeEach(() => vi.clearAllMocks())

  it('stays idle with no column selected', () => {
    const { result } = renderHook(() => useColumnHistory(null))

    expect(vi.mocked(getColumnHistory)).not.toHaveBeenCalled()
    expect(result.current.changes).toEqual([])
    expect(result.current.isLoading).toBe(false)
  })

  it('loads the selected column history', async () => {
    vi.mocked(getColumnHistory).mockResolvedValue(page([makeChange('h1', 'Description')], 7))

    const { result } = renderHook(() => useColumnHistory('c1'))

    await waitFor(() => expect(result.current.changes).toHaveLength(1))
    expect(result.current.total).toBe(7)
    expect(result.current.error).toBeNull()
    expect(vi.mocked(getColumnHistory)).toHaveBeenCalledWith('c1', 50)
  })

  it('reports a failure rather than showing an empty history', async () => {
    vi.mocked(getColumnHistory).mockRejectedValue(new Error('Boom'))

    const { result } = renderHook(() => useColumnHistory('c1'))

    await waitFor(() => expect(result.current.error).toBe('Boom'))
    expect(result.current.isLoading).toBe(false)
  })

  it('drops a response for a column that is no longer selected', async () => {
    const first = deferred<PagedResult<MetadataChange>>()
    const second = deferred<PagedResult<MetadataChange>>()
    vi.mocked(getColumnHistory)
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)

    const { result, rerender } = renderHook(({ id }: { id: string }) => useColumnHistory(id), {
      initialProps: { id: 'c1' },
    })

    rerender({ id: 'c2' })

    await act(async () => {
      second.resolve(page([makeChange('h2', 'Owner')]))
      await second.promise
    })
    expect(result.current.changes[0].id).toBe('h2')

    // The first column's history lands last. Taking it would show one column's history under
    // another's name.
    await act(async () => {
      first.resolve(page([makeChange('h1', 'Description')]))
      await first.promise
    })

    expect(result.current.changes[0].id).toBe('h2')
  })
})
