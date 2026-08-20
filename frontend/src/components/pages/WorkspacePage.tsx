import { useState, useEffect, useRef } from 'react'
import { useSession } from '../../hooks/useSession'
import { useCoverage } from '../../hooks/useCoverage'
import { useMetadataColumns } from '../../hooks/useMetadataColumns'
import { useBusinessTerms } from '../../hooks/useBusinessTerms'
import { useTableNames } from '../../hooks/useTableNames'
import { useBulkUpdate } from '../../hooks/useBulkUpdate'
import { mapTermToColumn } from '../../services/businessTermService'
import type { ColumnUpdateRequest } from '../../types/api'
import CoverageBanner from '../coverage/CoverageBanner'
import GridToolbar from '../grid/GridToolbar'
import MetadataGrid from '../grid/MetadataGrid'
import CsvUploadModal from '../upload/CsvUploadModal'
import BusinessTermsPanel from '../terms/BusinessTermsPanel'
import LoadingSpinner from '../ui/LoadingSpinner'
import ErrorMessage from '../ui/ErrorMessage'

export default function WorkspacePage() {
  const { session } = useSession()

  const [search, setSearch] = useState('')
  const [undocumentedOnly, setUndocumentedOnly] = useState(false)
  const [tableName, setTableName] = useState('')
  const [uploadOpen, setUploadOpen] = useState(false)
  const [termsOpen, setTermsOpen] = useState(false)

  const { coverage, reload: reloadCoverage } = useCoverage()
  const { columns, isLoading, error, reload: reloadColumns } = useMetadataColumns({
    search,
    undocumented_only: undocumentedOnly,
    table_name: tableName || undefined,
  })
  const { terms, isLoading: termsLoading, error: termsError, create: createTerm } = useBusinessTerms()
  const { tableNames, reload: reloadTableNames } = useTableNames()
  const { mutate } = useBulkUpdate()

  const didDefaultTable = useRef(false)
  useEffect(() => {
    if (!didDefaultTable.current && tableNames.length > 0) {
      setTableName(tableNames[0])
      didDefaultTable.current = true
    }
  }, [tableNames])

  async function handleUpdate(update: ColumnUpdateRequest) {
    await mutate([update])
    reloadCoverage()
  }

  async function handleTermMap(columnId: string, termId: string) {
    await mapTermToColumn({ termId, columnId })
    reloadColumns()
    reloadCoverage()
  }

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
            onUpdate={handleUpdate}
            onTermMap={handleTermMap}
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
