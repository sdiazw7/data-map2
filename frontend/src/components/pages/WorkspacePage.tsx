import { useCallback, useState } from 'react'
import { useSession } from '../../hooks/useSession'
import { useCoverage } from '../../hooks/useCoverage'
import { useMetadataColumns } from '../../hooks/useMetadataColumns'
import { useBusinessTerms } from '../../hooks/useBusinessTerms'
import { useTableNames } from '../../hooks/useTableNames'
import type { ColumnEdit, ColumnEdits } from '../../utils/columnFields'
import { setColumnBusinessTerm, clearColumnBusinessTerm } from '../../services/businessTermService'
import type { SortField, SortDir } from '../../services/metadataService'
import { ApiError, ApiErrorCode } from '../../utils/api'
import CoverageBanner from '../coverage/CoverageBanner'
import GridToolbar from '../grid/GridToolbar'
import MetadataGrid from '../grid/MetadataGrid'
import CsvUploadModal from '../upload/CsvUploadModal'
import BusinessTermsPanel from '../terms/BusinessTermsPanel'
import LoadingSpinner from '../ui/LoadingSpinner'
import ErrorMessage from '../ui/ErrorMessage'
import Toast from '../ui/Toast'
import type { ToastVariant } from '../ui/Toast'

/** Explains a rejected edit in terms of what happened to it, since it has already been undone. */
function editErrorMessage(err: unknown): string {
  if (err instanceof ApiError && err.code === ApiErrorCode.VersionConflict) {
    return 'Someone else edited this column first, so your change was not saved. The grid has been refreshed with their version — reapply your edit if it still applies.'
  }
  return err instanceof Error ? err.message : 'Your change could not be saved.'
}

/**
 * The same for a pasted range. A paste can fail for some rows and land for the rest, so this
 * cannot claim the whole paste was lost the way the single-cell message does.
 */
function pasteErrorMessage(err: unknown): string {
  if (err instanceof ApiError && err.code === ApiErrorCode.VersionConflict) {
    return 'Some pasted cells were not saved, because those columns changed since you loaded them. The rest were saved, and the grid has been refreshed — paste again over the rows that were put back.'
  }
  return err instanceof Error ? err.message : 'Some pasted cells could not be saved.'
}

export default function WorkspacePage() {
  const { session } = useSession()

  const [search, setSearch] = useState('')
  const [undocumentedOnly, setUndocumentedOnly] = useState(false)
  const [tableName, setTableName] = useState('')
  const [sortBy, setSortBy] = useState<SortField>('columnName')
  const [sortDir, setSortDir] = useState<SortDir>('asc')
  const [uploadOpen, setUploadOpen] = useState(false)
  const [termsOpen, setTermsOpen] = useState(false)
  const [toast, setToast] = useState<{ message: string; variant: ToastVariant } | null>(null)

  const { coverage, reload: reloadCoverage } = useCoverage()
  const {
    columns,
    total,
    isLoading,
    isLoadingMore,
    error,
    loadMore,
    reload: reloadColumns,
    editColumn,
    editColumns,
    applyTerm,
  } = useMetadataColumns({
    search,
    undocumentedOnly,
    tableName: tableName || undefined,
    sortBy,
    sortDir,
  })
  const { terms, isLoading: termsLoading, error: termsError, create: createTerm } = useBusinessTerms()
  const { tableNames, reload: reloadTableNames } = useTableNames()

  // The grid calls these from cell handlers that cannot await, so neither may reject — a
  // rejection here would be an unhandled one, which is how a failed edit used to disappear.
  //
  // Wrapped so their identity survives a re-render: the grid builds its column definitions from
  // them, and a new function each render would rebuild every definition on every keystroke.
  const handleEdit = useCallback(
    async (columnId: string, edit: ColumnEdit) => {
      try {
        await editColumn(columnId, edit)

        // Coverage is a server-side aggregate, so it is reread rather than adjusted locally.
        reloadCoverage()
      } catch (err: unknown) {
        setToast({ message: editErrorMessage(err), variant: 'error' })
      }
    },
    [editColumn, reloadCoverage],
  )

  const handleTermMap = useCallback(async (columnId: string, termId: string) => {
    // The empty option in the term cell means "no term", which is a delete, not a mapping.
    const termName = termId ? terms.find(t => t.id === termId)?.name ?? null : null

    // Applied first so the cell moves with the click; applyTerm hands back what it replaced.
    const previous = applyTerm(columnId, termName)

    try {
      const result = termId
        ? await setColumnBusinessTerm(columnId, termId)
        : await clearColumnBusinessTerm(columnId)

      // The mapping moved the row's version. Without taking the new one the next edit to this
      // row would spend a version the server has already retired, and be rejected as stale.
      applyTerm(columnId, termName, result.version)

      reloadCoverage()
    } catch (err: unknown) {
      applyTerm(columnId, previous)
      setToast({ message: editErrorMessage(err), variant: 'error' })
    }
  }, [terms, applyTerm, reloadCoverage])

  const handlePasteEdits = useCallback(
    async (edits: ColumnEdits, skippedRows: number) => {
      // Pasted rows that fell past the end of the loaded window. Saying so matters more than
      // for a single edit: the user pasted a block and can only see part of where it landed.
      const truncated =
        skippedRows > 0
          ? `${skippedRows.toLocaleString()} pasted ${skippedRows === 1 ? 'row' : 'rows'} fell past the columns loaded so far and were not applied. Scroll further down to load them, then paste again.`
          : null

      if (edits.length === 0) {
        if (truncated) setToast({ message: truncated, variant: 'notice' })
        return
      }

      try {
        await editColumns(edits)
        reloadCoverage()

        if (truncated) setToast({ message: truncated, variant: 'notice' })
      } catch (err: unknown) {
        setToast({ message: pasteErrorMessage(err), variant: 'error' })
      }
    },
    [editColumns, reloadCoverage],
  )

  // Depends on sortBy, so it does change identity when the sort does — but the column
  // definitions are rebuilt on a sort change anyway, to move the direction arrow.
  const handleSortChange = useCallback(
    (field: SortField) => {
      if (field === sortBy) {
        setSortDir(dir => (dir === 'asc' ? 'desc' : 'asc'))
      } else {
        setSortBy(field)
        setSortDir('asc')
      }
    },
    [sortBy],
  )

  if (!session) {
    return (
    <div className="flex items-center justify-center h-screen text-gray-500">
      No active session. Please use your invite link to join a workspace
    </div>
    )
  }

  return (
    <div className="flex flex-col flex-1 overflow-hidden">
      {coverage && (
        <div className="shrink-0">
          <CoverageBanner coverage={coverage} />
        </div>
      )}

      <div className="shrink-0">
        <GridToolbar
          onSearchChange={setSearch}
          onUndocumentedOnlyChange={setUndocumentedOnly}
          onUploadClick={() => setUploadOpen(true)}
          onBusinessTermsClick={() => setTermsOpen(true)}
          tableNames={tableNames}
          selectedTable={tableName}
          onTableChange={setTableName}
          loadedCount={columns.length}
          totalCount={total}
        />
      </div>

      <div className="flex-1 relative overflow-hidden">
        {isLoading && (
          <div className="absolute inset-0 flex items-center justify-center bg-white/70 z-10">
            <LoadingSpinner />
          </div>
        )}
        {error && !isLoading && (
          <div className="flex items-center justify-center h-full">
            <ErrorMessage message={error} />
          </div>
        )}
        {!error && (
          <MetadataGrid
            columns={columns}
            terms={terms}
            onEdit={handleEdit}
            total={total}
            onLoadMore={loadMore}
            isLoadingMore={isLoadingMore}
            onTermMap={handleTermMap}
          onPasteEdits={handlePasteEdits}
            sortBy={sortBy}
            sortDir={sortDir}
            onSortChange={handleSortChange}
          />
        )}
      </div>

      {uploadOpen && (
        <CsvUploadModal
          onClose={() => setUploadOpen(false)}
          onSuccess={() => {
            reloadColumns()
            reloadCoverage()
            reloadTableNames()
          }}
        />
      )}

      {toast && (
        <Toast message={toast.message} variant={toast.variant} onDismiss={() => setToast(null)} />
      )}

      {termsOpen && (
        <div className="fixed inset-0 z-50 flex justify-end">
          <div
            className="absolute inset-0 bg-black/50"
            onClick={() => setTermsOpen(false)}
          />
          <div className="relative w-full max-w-sm bg-white shadow-xl overflow-y-auto">
            <BusinessTermsPanel
              terms={terms}
              isLoading={termsLoading}
              error={termsError}
              create={createTerm}
              onClose={() => setTermsOpen(false)}
            />
          </div>
        </div>
      )}
    </div>
  )
}
