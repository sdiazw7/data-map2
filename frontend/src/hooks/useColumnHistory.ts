import { useState, useEffect } from 'react'
import type { MetadataChange } from '../types/api'
import { getColumnHistory } from '../services/metadataService'

type UseColumnHistoryResult = {
  changes: MetadataChange[]
  /** Recorded edits across all pages, so the panel can say when it is showing only some. */
  total: number
  isLoading: boolean
  error: string | null
}

/** How many entries the panel asks for. Deep history is read rarely; the recent end is the point. */
const HISTORY_PAGE_SIZE = 50

/**
 * One column's recorded edits. Passing null — no column selected — leaves the hook idle rather
 * than fetching, so the panel can mount before there is anything to show.
 */
export function useColumnHistory(columnId: string | null): UseColumnHistoryResult {
  const [changes, setChanges] = useState<MetadataChange[]>([])
  const [total, setTotal] = useState(0)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!columnId) {
      setChanges([])
      setTotal(0)
      setError(null)
      return
    }

    // Selecting another column while this one is still loading would otherwise let the slower
    // response overwrite the newer one.
    let current = true

    setIsLoading(true)
    setError(null)

    getColumnHistory(columnId, HISTORY_PAGE_SIZE)
      .then(page => {
        if (!current) return
        setChanges(page.items)
        setTotal(page.total)
      })
      .catch((err: unknown) => {
        if (!current) return
        setError(err instanceof Error ? err.message : 'Failed to load the change history.')
      })
      .finally(() => {
        if (current) setIsLoading(false)
      })

    return () => {
      current = false
    }
  }, [columnId])

  return { changes, total, isLoading, error }
}
