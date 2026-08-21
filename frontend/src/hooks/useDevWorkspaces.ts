import { useState, useEffect } from 'react'
import type { JoinResponse, WorkspaceSummary } from '../types/api'
import { getDevWorkspaces, joinDevWorkspace } from '../services/devService'

export function useDevWorkspaces(): {
  workspaces: WorkspaceSummary[]
  isLoading: boolean
  error: string | null
  join: (id: string) => Promise<JoinResponse>
} {
  const [workspaces, setWorkspaces] = useState<WorkspaceSummary[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setIsLoading(true)
    setError(null)

    getDevWorkspaces()
      .then(page => setWorkspaces(page.items))
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : 'Failed to load workspaces.')
      })
      .finally(() => setIsLoading(false))
  }, [])

  function join(id: string): Promise<JoinResponse> {
    return joinDevWorkspace(id)
  }

  return { workspaces, isLoading, error, join }
}
