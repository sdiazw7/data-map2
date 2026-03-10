import { useState, useEffect } from 'react'
import type { InviteDto } from '../types/api'
import { getInvite } from '../services/inviteService'

export function useInvite(token: string): {
  invite: InviteDto | null
  isLoading: boolean
  error: string | null
} {
  const [invite, setInvite] = useState<InviteDto | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!token) return

    setInvite(null)
    setError(null)
    setIsLoading(true)

    getInvite(token)
      .then(setInvite)
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : 'Failed to load invite.')
      })
      .finally(() => setIsLoading(false))
  }, [token])

  return { invite, isLoading, error }
}
