import { useEffect, useMemo, useRef } from 'react'
import type { Row } from '@tanstack/react-table'
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import { useVirtualizer } from '@tanstack/react-virtual'
import type { ColumnGridRow, BusinessTermDto } from '../../types/api'
import type { ColumnEdit, ColumnEdits, EditableField } from '../../utils/columnFields'
import type { SortField, SortDir } from '../../services/metadataService'
import { useGridSelection } from '../../hooks/useGridSelection'
import { buildPasteEdits, parseClipboardGrid } from '../../utils/clipboard'
import GridCell from './GridCell'
import BusinessTermCell from './BusinessTermCell'

type Props = {
  columns: ColumnGridRow[]
  terms: BusinessTermDto[]
  onEdit: (columnId: string, edit: ColumnEdit) => void
  /** Rows matching the filters across all pages; the loaded ones are in `columns`. */
  total: number
  /** Asked for as the user nears the end of what is loaded. */
  onLoadMore: () => void
  isLoadingMore: boolean
  onTermMap: (columnId: string, termId: string, termName: string | null) => void
  /**
   * A pasted range, already laid over the loaded rows. `skippedRows` counts the pasted rows
   * that fell past the end of the window and so had nowhere to go.
   */
  onPasteEdits: (edits: ColumnEdits, skippedRows: number) => void
  /** The row the selection is on, so the page can offer actions scoped to it. */
  onActiveRowChange: (columnId: string | null) => void
  sortBy: SortField
  sortDir: SortDir
  onSortChange: (field: SortField) => void
}

const columnHelper = createColumnHelper<ColumnGridRow>()

/**
 * How close to the last loaded row the viewport gets before the next page is requested. Two
 * screens' worth, so the fetch is usually done before the user reaches the rows it holds.
 */
const LOAD_MORE_THRESHOLD = 40

export default function MetadataGrid({
  columns: rows,
  terms,
  onEdit,
  onTermMap,
  onPasteEdits,
  onActiveRowChange,
  total,
  onLoadMore,
  isLoadingMore,
  sortBy,
  sortDir,
  onSortChange,
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null)

  const { active, isEditing, editSeed, activate, startEdit, stopEdit, handleKeyDown } =
    useGridSelection({ rowCount: rows.length })

  // Rebuilt when the selection moves, which is bounded by the viewport: TanStack keys its row
  // model on `data`, so moving the cursor re-renders the cells on screen but does not walk the
  // window the way an edit does.
  const tableColumns = useMemo(() => {
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

    function editableCell(field: EditableField, row: Row<ColumnGridRow>) {
      const isActiveCell = active?.rowIndex === row.index && active.field === field

      return (
        <GridCell
          value={row.original[field]}
          isActive={isActiveCell}
          isEditing={isActiveCell && isEditing}
          editSeed={editSeed}
          onActivate={() => activate({ rowIndex: row.index, field })}
          onStartEdit={() => {
            activate({ rowIndex: row.index, field })
            startEdit()
          }}
          onCommit={(value, move) => {
            // Assigned rather than built from a computed key, which would widen the type to a
            // string index signature and lose the field names.
            const edit: ColumnEdit = {}
            edit[field] = value === '' ? null : value

            onEdit(row.original.columnId, edit)
            stopEdit(move)
          }}
          onCancel={move => stopEdit(move)}
        />
      )
    }

    return [
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
        cell: ({ row }) => editableCell('description', row),
      }),
      columnHelper.display({
        id: 'exampleValue',
        header: 'Example',
        cell: ({ row }) => editableCell('exampleValue', row),
      }),
      columnHelper.display({
        id: 'owner',
        header: () => sortableHeader('Owner', 'owner'),
        cell: ({ row }) => editableCell('owner', row),
      }),
      columnHelper.display({
        id: 'businessTerm',
        header: 'Business Term',
        cell: ({ row }) => (
          <BusinessTermCell
            value={row.original.businessTerm}
            terms={terms}
            onChange={(termId, termName) => onTermMap(row.original.columnId, termId, termName)}
          />
        ),
      }),
    ]
  }, [
    terms,
    onEdit,
    onTermMap,
    sortBy,
    sortDir,
    onSortChange,
    active,
    isEditing,
    editSeed,
    activate,
    startEdit,
    stopEdit,
  ])

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

  // The window extends from what is actually on screen rather than from a scroll offset, so it
  // also covers the case where the first page does not fill the viewport and no scroll ever
  // happens.
  const lastVisibleIndex = virtualRows.length > 0 ? virtualRows[virtualRows.length - 1].index : 0

  useEffect(() => {
    if (rows.length === 0 || rows.length >= total) return
    if (lastVisibleIndex >= rows.length - LOAD_MORE_THRESHOLD) onLoadMore()
  }, [lastVisibleIndex, rows.length, total, onLoadMore])

  // Arrowing past the edge of the viewport has to bring the row with it — under virtualization
  // the row the selection moved to may not be mounted at all.
  useEffect(() => {
    if (active) virtualizer.scrollToIndex(active.rowIndex, { align: 'auto' })
    // The virtualizer is a fresh object each render; following the row is what matters.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [active?.rowIndex])

  const activeColumnId = active ? rows[active.rowIndex]?.columnId ?? null : null

  useEffect(() => {
    onActiveRowChange(activeColumnId)
  }, [activeColumnId, onActiveRowChange])

  // The editor takes focus while it is open, and the grid needs it back afterwards or the next
  // arrow key goes to the document. preventScroll because the scroll position is the
  // virtualizer's to decide.
  useEffect(() => {
    if (active && !isEditing) containerRef.current?.focus({ preventScroll: true })
  }, [active, isEditing])

  function handlePaste(event: React.ClipboardEvent) {
    // With the editor open this is an ordinary paste into a text box.
    if (!active || isEditing) return

    const text = event.clipboardData.getData('text/plain')
    if (!text) return

    const grid = parseClipboardGrid(text)
    if (grid.length === 0) return

    event.preventDefault()

    const { edits, skippedRows } = buildPasteEdits(rows, active, grid)
    onPasteEdits(edits, skippedRows)
  }

  if (rows.length === 0) {
    return (
      <div className="flex items-center justify-center h-full text-gray-500 text-sm">
        No columns found.
      </div>
    )
  }

  return (
    <div
      ref={containerRef}
      tabIndex={0}
      onKeyDown={event => {
        // Tab included: the grid moves between cells rather than letting focus leave it.
        if (handleKeyDown(event)) event.preventDefault()
      }}
      onPaste={handlePaste}
      className="overflow-auto h-full outline-none"
    >
      <table role="grid" aria-rowcount={total} className="w-full text-sm border-collapse">
        <thead className="sticky top-0 bg-gray-50 z-10">
          {table.getHeaderGroups().map(headerGroup => (
            <tr key={headerGroup.id} role="row">
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
            <tr role="presentation">
              <td style={{ height: `${paddingTop}px` }} colSpan={tableColumns.length} />
            </tr>
          )}
          {virtualRows.map(virtualRow => {
            const row = tableRows[virtualRow.index]
            return (
              <tr
                key={row.id}
                role="row"
                aria-rowindex={virtualRow.index + 1}
                className="border-b border-gray-100 hover:bg-gray-50"
                style={{ height: `${virtualRow.size}px` }}
              >
                {row.getVisibleCells().map(cell => (
                  <td
                    key={cell.id}
                    role="gridcell"
                    aria-selected={
                      active?.rowIndex === virtualRow.index && active.field === cell.column.id
                    }
                    className="px-3 py-1 align-middle"
                  >
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </td>
                ))}
              </tr>
            )
          })}
          {paddingBottom > 0 && (
            <tr role="presentation">
              <td style={{ height: `${paddingBottom}px` }} colSpan={tableColumns.length} />
            </tr>
          )}
        </tbody>
      </table>
      {isLoadingMore && (
        <div className="py-3 text-center text-sm text-gray-500" aria-live="polite">
          Loading more columns...
        </div>
      )}
    </div>
  )
}
