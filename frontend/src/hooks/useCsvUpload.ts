import { useState } from 'react'
import type { ImportSummary } from '../types/api'
import { importCsv } from '../services/metadataService'

type UseCsvUploadResult = {
  upload: (file: File) => Promise<ImportSummary>
  isUploading: boolean
  error: string | null
}

export function useCsvUpload(): UseCsvUploadResult {
  const [isUploading, setIsUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function upload(file: File): Promise<ImportSummary> {
    setIsUploading(true)
    setError(null)
    try {
      return await importCsv(file)
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Upload failed.'
      setError(message)
      throw err
    } finally {
      setIsUploading(false)
    }
  }

  return { upload, isUploading, error }
}
