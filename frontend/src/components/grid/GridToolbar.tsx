import { useState, useEffect } from 'react'

type Props = {
  onSearchChange: (value: string) => void
  onUndocumentedOnlyChange: (value: boolean) => void
  onUploadClick: () => void
  onBusinessTermsClick: () => void
  onHistoryClick: () => void
  /** History is scoped to one column, so there is nothing to open without a selected cell. */
  canShowHistory: boolean
  tableNames: string[]
  selectedTable: string
  onTableChange: (value: string) => void
  /** Rows currently loaded in the grid. */
  loadedCount: number
  /** Rows matching the filters across all pages. */
  totalCount: number
}

export default function GridToolbar({
  onSearchChange,
  onUndocumentedOnlyChange,
  onUploadClick,
  onBusinessTermsClick,
  onHistoryClick,
  canShowHistory,
  tableNames,
  selectedTable,
  onTableChange,
  loadedCount,
  totalCount,
}: Props) {
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
      <select
        value={selectedTable}
        onChange={e => onTableChange(e.target.value)}
        className="px-3 py-1.5 border border-gray-300 rounded text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500"
      >
        <option value="">All tables</option>
        {tableNames.map(name => (
          <option key={name} value={name}>
            {name}
          </option>
        ))}
      </select>
      <span className="text-sm text-gray-500 tabular-nums whitespace-nowrap">
        {loadedCount < totalCount
          ? `Showing ${loadedCount.toLocaleString()} of ${totalCount.toLocaleString()}`
          : `${totalCount.toLocaleString()} ${totalCount === 1 ? 'column' : 'columns'}`}
      </span>
      <div className="ml-auto flex items-center gap-2">
        <button
          type="button"
          onClick={onHistoryClick}
          disabled={!canShowHistory}
          title={canShowHistory ? undefined : 'Select a cell to see that column’s history'}
          className="px-3 py-1.5 border border-gray-300 text-gray-700 text-sm rounded hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-white"
        >
          History
        </button>
        <button
          type="button"
          onClick={onBusinessTermsClick}
          className="px-3 py-1.5 border border-gray-300 text-gray-700 text-sm rounded hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          Business Terms
        </button>
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
