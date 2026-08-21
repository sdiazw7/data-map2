import { useRef } from 'react'
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import { useVirtualizer } from '@tanstack/react-virtual'
import type { ColumnGridRow, BusinessTermDto } from '../../types/api'
import type { ColumnEdit } from '../../hooks/useMetadataColumns'
import type { SortField, SortDir } from '../../services/metadataService'
import GridCell from './GridCell'
import BusinessTermCell from './BusinessTermCell'

type Props = {
  columns: ColumnGridRow[]
  terms: BusinessTermDto[]
  onEdit: (columnId: string, edit: ColumnEdit) => void
  onTermMap: (columnId: string, termId: string) => void
  sortBy: SortField
  sortDir: SortDir
  onSortChange: (field: SortField) => void
}

const columnHelper = createColumnHelper<ColumnGridRow>()

export default function MetadataGrid({ columns: rows, terms, onEdit, onTermMap, sortBy, sortDir, onSortChange }: Props) {
  const containerRef = useRef<HTMLDivElement>(null)

  function sortableHeader(label: string, field: SortField) {
    const isActive = sortBy === field
    return (
      <button
        type="button"
        onClick={() => onSortChange(field)}
        className="flex items-center gap-1 font-semibold uppercase tracking-wider hover:text-gray-900"
        aria-label={`Sort by ${label}`}
      >
        {label}
        {isActive && <span aria-hidden="true">{sortDir === 'asc' ? '▲' : '▼'}</span>}
      </button>
    )
  }

  const tableColumns = [
    columnHelper.accessor('schemaName', {
      header: 'Schema',
      cell: info => info.getValue() ?? '',
    }),
    columnHelper.accessor('tableName', {
      header: () => sortableHeader('Table', 'tableName'),
      cell: info => info.getValue() ?? '',
    }),
    columnHelper.accessor('columnName', {
      header: () => sortableHeader('Column', 'columnName'),
      cell: info => info.getValue() ?? '',
    }),
    columnHelper.accessor('dataType', {
      header: () => sortableHeader('Type', 'dataType'),
      cell: info => info.getValue() ?? '',
    }),
    columnHelper.display({
      id: 'description',
      header: 'Description',
      cell: ({ row }) => (
        <GridCell
          value={row.original.description}
          onChange={val => onEdit(row.original.columnId, { description: val || null })}
        />
      ),
    }),
    columnHelper.display({
      id: 'exampleValue',
      header: 'Example',
      cell: ({ row }) => (
        <GridCell
          value={row.original.exampleValue}
          onChange={val => onEdit(row.original.columnId, { exampleValue: val || null })}
        />
      ),
    }),
    columnHelper.display({
      id: 'owner',
      header: () => sortableHeader('Owner', 'owner'),
      cell: ({ row }) => (
        <GridCell
          value={row.original.owner}
          onChange={val => onEdit(row.original.columnId, { owner: val || null })}
        />
      ),
    }),
    columnHelper.display({
      id: 'businessTerm',
      header: 'Business Term',
      cell: ({ row }) => (
        <BusinessTermCell
          value={row.original.businessTerm}
          terms={terms}
          onChange={termId => onTermMap(row.original.columnId, termId)}
        />
      ),
    }),
  ]

  const table = useReactTable({
    data: rows,
    columns: tableColumns,
    getCoreRowModel: getCoreRowModel(),
    // Without this the row id is its index, so React keys cells by screen position. A cell
    // open for editing would stay mounted across a re-sort, a filter change, or the reload a
    // version conflict triggers, and commit its draft into whichever column took that slot.
    getRowId: row => row.columnId,
  })

  const { rows: tableRows } = table.getRowModel()

  const virtualizer = useVirtualizer({
    count: tableRows.length,
    getScrollElement: () => containerRef.current,
    estimateSize: () => 36,
    overscan: 10,
  })

  const virtualRows = virtualizer.getVirtualItems()
  const totalSize = virtualizer.getTotalSize()
  const paddingTop = virtualRows.length > 0 ? virtualRows[0].start : 0
  const paddingBottom =
    virtualRows.length > 0 ? totalSize - virtualRows[virtualRows.length - 1].end : 0

  if (rows.length === 0) {
    return (
      <div className="flex items-center justify-center h-full text-gray-500 text-sm">
        No columns found.
      </div>
    )
  }

  return (
    <div ref={containerRef} className="overflow-auto h-full">
      <table className="w-full text-sm border-collapse">
        <thead className="sticky top-0 bg-gray-50 z-10">
          {table.getHeaderGroups().map(headerGroup => (
            <tr key={headerGroup.id}>
              {headerGroup.headers.map(header => (
                <th
                  key={header.id}
                  className="px-3 py-2 text-left text-xs font-semibold text-gray-600 uppercase tracking-wider border-b border-gray-200 whitespace-nowrap"
                >
                  {flexRender(header.column.columnDef.header, header.getContext())}
                </th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody>
          {paddingTop > 0 && (
            <tr>
              <td style={{ height: `${paddingTop}px` }} colSpan={tableColumns.length} />
            </tr>
          )}
          {virtualRows.map(virtualRow => {
            const row = tableRows[virtualRow.index]
            return (
              <tr
                key={row.id}
                className="border-b border-gray-100 hover:bg-gray-50"
                style={{ height: `${virtualRow.size}px` }}
              >
                {row.getVisibleCells().map(cell => (
                  <td key={cell.id} className="px-3 py-1 align-middle">
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </td>
                ))}
              </tr>
            )
          })}
          {paddingBottom > 0 && (
            <tr>
              <td style={{ height: `${paddingBottom}px` }} colSpan={tableColumns.length} />
            </tr>
          )}
        </tbody>
      </table>
    </div>
  )
}
