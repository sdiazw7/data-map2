import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useDevWorkspaces } from '../../hooks/useDevWorkspaces'
import { useSession } from '../../hooks/useSession'
import LoadingSpinner from '../ui/LoadingSpinner'
import ErrorMessage from '../ui/ErrorMessage'

// Dev-only landing page: lets a local developer jump into a seeded workspace
// without going through the invite flow. The backing /dev/workspaces endpoint
// only exists when the API is running in Development.
export default function WorkspacePickerPage() {
  const { workspaces, isLoading, error, join } = useDevWorkspaces()
  const { saveSession } = useSession()
  const navigate = useNavigate()

  const [joiningId, setJoiningId] = useState<string | null>(null)
  const [joinError, setJoinError] = useState<string | null>(null)

  async function handleSelect(id: string) {
    setJoiningId(id)
    setJoinError(null)
    try {
      const result = await join(id)
      saveSession(result)
      navigate('/workspace')
    } catch (err: unknown) {
      setJoinError(err instanceof Error ? err.message : 'Failed to join workspace.')
    } finally {
      setJoiningId(null)
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <LoadingSpinner />
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-gray-600">
          No workspace picker available here. Please use your invite link to get started.
        </p>
      </div>
    )
  }

  if (workspaces.length === 0) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-gray-600">No workspaces found. Seed the database or use your invite link.</p>
      </div>
    )
  }

  return (
    <div className="flex items-center justify-center min-h-screen bg-gray-50">
      <div className="bg-white shadow rounded-lg p-8 w-full max-w-md">
        <h1 className="text-xl font-semibold text-gray-900 mb-1">Pick a workspace</h1>
        <p className="text-sm text-gray-500 mb-6">Dev mode — no invite required.</p>
        {joinError && <ErrorMessage message={joinError} />}
        <ul className="mt-2 space-y-2">
          {workspaces.map(w => (
            <li key={w.id}>
              <button
                type="button"
                onClick={() => handleSelect(w.id)}
                disabled={joiningId !== null}
                className="w-full text-left px-4 py-2 border border-gray-300 rounded text-sm hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
              >
                {joiningId === w.id ? 'Joining...' : w.name}
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
