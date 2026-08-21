import { useState } from 'react'
import type { BulkUpdateResponse, ColumnUpdateRequest } from '../types/api'
import { bulkUpdateColumns } from '../services/metadataService'

type UseBulkUpdateResult = {
  mutate: (updates: ColumnUpdateRequest[]) => Promise<BulkUpdateResponse>
  isLoading: boolean
}

export function useBulkUpdate(): UseBulkUpdateResult {
  const [isLoading, setIsLoading] = useState(false)

  async function mutate(updates: ColumnUpdateRequest[]): Promise<BulkUpdateResponse> {
    setIsLoading(true)
    try {
      return await bulkUpdateColumns(updates)
    } finally {
      setIsLoading(false)
    }
  }

  return { mutate, isLoading }
}
