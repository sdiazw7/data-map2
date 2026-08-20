import { useState } from 'react'
import type { BusinessTermDto, BusinessTermCreateRequest } from '../../types/api'
import LoadingSpinner from '../ui/LoadingSpinner'
import ErrorMessage from '../ui/ErrorMessage'

type Props = {
  terms: BusinessTermDto[]
  isLoading: boolean
  error: string | null
  create: (req: BusinessTermCreateRequest) => Promise<void>
  onClose: () => void
}

export default function BusinessTermsPanel({ terms, isLoading, error, create, onClose }: Props) {
  const [name, setName] = useState('')
  const [definition, setDefinition] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!name.trim()) {
      setFormError('Name is required.')
      return
    }
    setFormError(null)
    setIsSubmitting(true)
    try {
      await create({ name: name.trim(), definition: definition.trim() })
      setName('')
      setDefinition('')
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : 'Failed to create term.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="p-4">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-base font-semibold text-gray-900">Business Terms</h2>
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
      {error && <ErrorMessage message={error} />}

      {!isLoading && (
        <ul className="mb-6 space-y-2">
          {terms.length === 0 && (
            <li className="text-sm text-gray-500 italic">No terms defined yet.</li>
          )}
          {terms.map(term => (
            <li key={term.id} className="text-sm">
              <span className="font-medium text-gray-800">{term.name}</span>
              {term.definition && (
                <span className="text-gray-500 ml-2">— {term.definition}</span>
              )}
            </li>
          ))}
        </ul>
      )}

      <form onSubmit={handleSubmit} className="space-y-3 border-t border-gray-200 pt-4">
        <h3 className="text-sm font-medium text-gray-700">Add new term</h3>
        <div>
          <input
            type="text"
            placeholder="Name"
            value={name}
            onChange={e => setName(e.target.value)}
            disabled={isSubmitting}
            className="w-full px-3 py-1.5 border border-gray-300 rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
          />
        </div>
        <div>
          <input
            type="text"
            placeholder="Definition (optional)"
            value={definition}
            onChange={e => setDefinition(e.target.value)}
            disabled={isSubmitting}
            className="w-full px-3 py-1.5 border border-gray-300 rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
          />
        </div>
        {formError && <ErrorMessage message={formError} />}
        <button
          type="submit"
          disabled={isSubmitting}
          className="px-3 py-1.5 bg-blue-600 text-white text-sm rounded hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
        >
          {isSubmitting ? 'Adding...' : 'Add term'}
        </button>
      </form>
    </div>
  )
}
