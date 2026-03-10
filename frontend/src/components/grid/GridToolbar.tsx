import { useState, useEffect } from 'react'

type Props = {
  onSearchChange: (value: string) => void
  onUndocumentedOnlyChange: (value: boolean) => void
  onUploadClick: () => void
}

export default function GridToolbar({ onSearchChange, onUndocumentedOnlyChange, onUploadClick }: Props) {
  const [searchInput, setSearchInput] = useState('')

  useEffect(() => {
    const timer = setTimeout(() => {
      onSearchChange(searchInput)
    }, 300)
    return () => clearTimeout(timer)
  }, [searchInput, onSearchChange])

  return (
    <div className="flex items-center gap-4 px-4 py-2 border-b border-gray-200 bg-white">
      <input
        type="text"
        placeholder="Search columns..."
        value={searchInput}
        onChange={e => setSearchInput(e.target.value)}
        className="flex-1 max-w-sm px-3 py-1.5 border border-gray-300 rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
      />
      <label className="flex items-center gap-2 text-sm text-gray-600 cursor-pointer select-none">
        <input
          type="checkbox"
          onChange={e => onUndocumentedOnlyChange(e.target.checked)}
          className="rounded"
        />
        Undocumented only
      </label>
      <div className="ml-auto">
        <button
          type="button"
          onClick={onUploadClick}
          className="px-3 py-1.5 bg-blue-600 text-white text-sm rounded hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          Upload CSV
        </button>
      </div>
    </div>
  )
}
