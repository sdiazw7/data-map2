import { useRef } from 'react'
import { useCsvUpload } from '../../hooks/useCsvUpload'
import ErrorMessage from '../ui/ErrorMessage'

type Props = {
  onClose: () => void
  onSuccess: () => void
}

export default function CsvUploadModal({ onClose, onSuccess }: Props) {
  const fileRef = useRef<HTMLInputElement>(null)
  const { upload, isUploading, error } = useCsvUpload()

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const file = fileRef.current?.files?.[0]
    if (!file) return
    try {
      await upload(file)
      onSuccess()
      onClose()
    } catch {
      // error is shown via useCsvUpload error state
    }
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl p-6 w-full max-w-md">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">Upload CSV</h2>
        <form onSubmit={handleSubmit}>
          <div className="mb-4">
            <label htmlFor="csv-file" className="block text-sm font-medium text-gray-700 mb-1">
              CSV file
            </label>
            <input
              id="csv-file"
              ref={fileRef}
              type="file"
              accept=".csv"
              disabled={isUploading}
              className="w-full text-sm text-gray-600 file:mr-3 file:py-1.5 file:px-3 file:rounded file:border-0 file:text-sm file:bg-gray-100 file:text-gray-700 hover:file:bg-gray-200 disabled:opacity-50"
            />
          </div>
          {error && <ErrorMessage message={error} />}
          <div className="flex gap-3 justify-end mt-4">
            <button
              type="button"
              onClick={onClose}
              disabled={isUploading}
              className="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded hover:bg-gray-50 focus:outline-none disabled:opacity-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isUploading}
              className="px-4 py-2 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
            >
              {isUploading ? 'Uploading...' : 'Upload'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
