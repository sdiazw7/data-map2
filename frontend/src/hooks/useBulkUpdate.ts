import { useState } from 'react'
import type { ColumnUpdateRequest } from '../types/api'
import { bulkUpdateColumns } from '../services/metadataService'

type UseBulkUpdateResult = {
  mutate: (updates: ColumnUpdateRequest[]) => Promise<void>
  isLoading: boolean
}

export function useBulkUpdate(): UseBulkUpdateResult {
  const [isLoading, setIsLoading] = useState(false)

  async function mutate(updates: ColumnUpdateRequest[]): Promise<void> {
    setIsLoading(true)
    try {
      await bulkUpdateColumns(updates)
    } finally {
      setIsLoading(false)
    }
  }

  return { mutate, isLoading }
}
