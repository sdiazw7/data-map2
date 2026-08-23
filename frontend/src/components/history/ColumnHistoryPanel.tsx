import type { MetadataChange } from '../../types/api'
import { useColumnHistory } from '../../hooks/useColumnHistory'
import LoadingSpinner from '../ui/LoadingSpinner'
import ErrorMessage from '../ui/ErrorMessage'

type Props = {
  columnId: string
  /** Shown in the heading, so the panel says which column it is describing. */
  columnName: string
  onClose: () => void
}

/** The audit trail stores field names as the domain spells them; this is how they read. */
const FIELD_LABELS: Record<string, string> = {
  Description: 'Description',
  ExampleValue: 'Example value',
  Owner: 'Owner',
  BusinessTerm: 'Business term',
}

function formatWhen(iso: string): string {
  const at = new Date(iso)
  if (Number.isNaN(at.getTime())) return iso
  return at.toLocaleString()
}

function HistoryEntry({ change }: { change: MetadataChange }) {
  return (
    <li className="border-b border-gray-100 py-3 last:border-b-0">
      <div className="flex items-baseline justify-between gap-2">
        <span className="text-sm font-medium text-gray-900">
          {FIELD_LABELS[change.field] ?? change.field}
        </span>
        <time dateTime={change.editedAt} className="text-xs text-gray-500 whitespace-nowrap">
          {formatWhen(change.editedAt)}
        </time>
      </div>

      <div className="mt-1 text-sm">
        {change.oldValue === null ? (
          <span className="text-gray-400 italic">empty</span>
        ) : (
          <span className="text-gray-500 line-through">{change.oldValue}</span>
        )}
        <span aria-hidden="true" className="mx-2 text-gray-400">
          →
        </span>
        {change.newValue === null ? (
          <span className="text-gray-400 italic">cleared</span>
        ) : (
          <span className="text-gray-900">{change.newValue}</span>
        )}
      </div>

      <div className="mt-1 text-xs text-gray-500">{change.editedByEmail}</div>
    </li>
  )
}

export default function ColumnHistoryPanel({ columnId, columnName, onClose }: Props) {
  const { changes, total, isLoading, error } = useColumnHistory(columnId)

  return (
    <div className="p-4">
      <div className="flex items-start justify-between mb-4 gap-2">
        <div>
          <h2 className="text-base font-semibold text-gray-900">Change history</h2>
          <p className="text-sm text-gray-500 break-all">{columnName}</p>
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label="Close"
          className="text-gray-400 hover:text-gray-600 focus:outline-none"
        >
          &times;
        </button>
      </div>

      {isLoading && (
        <div className="flex justify-center py-4">
          <LoadingSpinner />
        </div>
      )}

      {error && !isLoading && <ErrorMessage message={error} />}

      {!isLoading && !error && changes.length === 0 && (
        <p className="text-sm text-gray-500">
          This column has not been edited yet. Edits made from the grid appear here.
        </p>
      )}

      {!isLoading && !error && changes.length > 0 && (
        <>
          <ul>
            {changes.map(change => (
              <HistoryEntry key={change.id} change={change} />
            ))}
          </ul>

          {changes.length < total && (
            <p className="mt-3 text-xs text-gray-500">
              Showing the {changes.length.toLocaleString()} most recent of{' '}
              {total.toLocaleString()} edits.
            </p>
          )}
        </>
      )}
    </div>
  )
}
